using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bezoro.Core.Common.Extensions;

namespace Bezoro.UCI.API;

/// <summary>
///     High-performance, thread–safe wrapper around a UCI engine process.
/// </summary>
public sealed class UCIEngine(Process process) : IAsyncDisposable
{
	private static readonly string[] DefaultTerminators =
	[
		"bestmove", "uciok", "readyok", "nodes searched", "checkers"
	];
	private readonly Channel<string> _out
		= Channel.CreateUnbounded<string>(
			new()
			{
				SingleWriter                  = true,
				AllowSynchronousContinuations = false
			});

	private readonly ConcurrentDictionary<PendingRequest, byte> _pending = new();

	private readonly ConcurrentQueue<string> _history     = new();
	private readonly Process                 _proc        = process ?? throw new ArgumentNullException(nameof(process));
	private readonly SemaphoreSlim           _commandLock = new(1, 1);
	private          int                     _disposed;

	private StreamWriter? _stdin;

	/// <summary>
	///     Waits until a line received from the engine contains the specified token
	///     (case-insensitive).  No command is sent – the caller is expected to have
	///     already issued the request that will eventually produce the token
	///     (“uciok”, “readyok”, custom search id, …).
	/// </summary>
	/// <param name="token">Substring that must appear in a single output line.</param>
	/// <param name="ct">Cancellation token to abort the wait.</param>
	public Task WaitForTokenAsync(string token, CancellationToken ct = default)
		=> WaitForTokensAsync([token], ct);

	/// <summary>
	///     Same as <see cref="WaitForTokenAsync(string,System.Threading.CancellationToken)" />
	///     but waits for any of the supplied tokens.
	/// </summary>
	public Task WaitForTokensAsync(string[] tokens, CancellationToken ct = default)
	{
		// Fail fast instead of silently completing when disposed.
		ThrowIfDisposed();

		var req = new PendingRequest(
			static (l, toks) =>
			{
				foreach (string t in toks)
				{
					if (l.AsSpan().Contains(t.AsSpan(), StringComparison.OrdinalIgnoreCase))
						return true;
				}

				return false;
			},
			tokens,
			ct);

		foreach (string line in _history)
		{
			if (req.TryAccept(line))
				return req.Task;
		}

		_pending.TryAdd(req, 0);
		return req.Task;
	}

	public async Task<List<string>> SendCommandAndReadOutputAsync(
		string            command,
		CancellationToken ct    = default,
		string[]?         until = null)
	{
		ThrowIfDisposed();

		await _commandLock.WaitAsync(ct).ConfigureAwait(false);

		try
		{
			until ??= DefaultTerminators;
			var req = new PendingRequest(
				static (l, tokens) =>
				{
					foreach (string tok in tokens)
					{
						if (l.AsSpan().Contains(
								tok.AsSpan(),
								StringComparison.OrdinalIgnoreCase))
							return true;
					}

					return false;
				},
				until,
				ct);

			_pending.TryAdd(req, 0);

			await WriteLineAsync(command, ct).ConfigureAwait(false);

			return await req.Task.ConfigureAwait(false);
		}
		finally
		{
			_commandLock.Release();
		}
	}

	public async ValueTask DisposeAsync()
	{
		// Fast-path: only run once
		if (Interlocked.Exchange(ref _disposed, 1) == 1)
			return;

		Exception? firstError = null;
		var        disposedEx = new ObjectDisposedException(nameof(UCIEngine));

		// Complete the output channel to unblock any readers
		try
		{
			_out.Writer.TryComplete(disposedEx);
		}
		catch (Exception ex)
		{
			firstError ??= ex;
		}

		// Best-effort: fail all pending requests so callers don't hang
		try
		{
			foreach (var kvp in _pending.Keys)
			{
				if (_pending.TryRemove(kvp, out _))
				{
					try
					{
						kvp.Fail(disposedEx);
					}
					catch (Exception ex)
					{
						firstError ??= ex;
					}
				}
			}
		}
		catch (Exception ex)
		{
			firstError ??= ex;
		}

		// Close stdin (flush first), then dispose
		try
		{
			var sw = Interlocked.Exchange(ref _stdin, null);

			if (sw is not null)
			{
				try
				{
					await sw.FlushAsync().ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					firstError ??= ex;
				}

				try
				{
					sw.Dispose();
				}
				catch (Exception ex)
				{
					firstError ??= ex;
				}
			}
		}
		catch (Exception ex)
		{
			firstError ??= ex;
		}

		// Best-effort cleanup of internal state
		try
		{
			_history.Clear();
		}
		catch (Exception ex)
		{
			firstError ??= ex;
		}

		// Dispose the command lock to prevent further use
		try
		{
			_commandLock.Dispose();
		}
		catch (Exception ex)
		{
			firstError ??= ex;
		}

		// Log (do not throw from Dispose)
		if (firstError is not null) Logger.LogError(firstError, this, LogCategory.UCI);
	}

	public ValueTask WriteLineAsync(string command, CancellationToken ct = default)
	{
		ThrowIfDisposed();
		Logger.LogInfo($"[INPUT] {command.Bold()}", this, LogCategory.UCI);

		// Harden against a race with DisposeAsync nulling _stdin after the disposed check.
		var sw = Volatile.Read(ref _stdin);

		if (sw is null)
		{
			// Ensure a consistent exception type
			throw new ObjectDisposedException(nameof(UCIEngine));
		}

		return new(sw.WriteLineAsync(command).WaitAsync(ct));
	}

	public void Start()
	{
		ThrowIfDisposed();

		_proc.Start();
		_stdin = _proc.StandardInput;

		_ = Task.Run(ReadLoop);
	}

	private async Task ReadLoop()
	{
		using var stdout = _proc.StandardOutput;

		while (!_disposed.IsPositive() && await stdout.ReadLineAsync() is { } line)
		{
			Logger.LogInfo($"[OUTPUT] {line.Bold()}", this, LogCategory.UCI);

			_history.Enqueue(line);
			if (_history.Count > 1_000)
				_history.TryDequeue(out _);

			_out.Writer.TryWrite(line);

			foreach (var kvp in _pending)
			{
				var req = kvp.Key;
				if (req.TryAccept(line))
					_pending.TryRemove(req, out _);
			}
		}
	}

	private void ThrowIfDisposed()
	{
		if (_disposed.IsPositive()) throw new ObjectDisposedException(nameof(UCIEngine));
	}

	private sealed class PendingRequest
	{
		private readonly CancellationTokenRegistration _ctr;
		private readonly Func<string, string[], bool>  _stop;
		private readonly List<string>                  _lines = [];
		private readonly string[]                      _tokens;
		private readonly TaskCompletionSource<List<string>> _tcs =
			new(TaskCreationOptions.RunContinuationsAsynchronously);

		public PendingRequest(
			Func<string, string[], bool> stop,
			string[]                     tokens,
			CancellationToken            ct)
		{
			_stop   = stop;
			_tokens = tokens;

			_ctr = ct.Register(
				static s => ((TaskCompletionSource<List<string>>)s!).TrySetCanceled(),
				_tcs);
		}

		public Task<List<string>> Task => _tcs.Task;

		public bool TryAccept(string line)
		{
			_lines.Add(line);
			if (!_stop(line, _tokens)) return false;

			_ctr.Dispose();
			_tcs.TrySetResult(_lines);
			return true;
		}

		public void Fail(Exception ex)
		{
			_ctr.Dispose();
			_tcs.TrySetException(ex);
		}
	}
}
