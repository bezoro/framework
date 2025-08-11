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
		= Channel.CreateBounded<string>(
			new BoundedChannelOptions(1)
			{
				SingleWriter                  = true,
				AllowSynchronousContinuations = false,
				FullMode                      = BoundedChannelFullMode.DropOldest
			});

	private readonly ConcurrentDictionary<PendingRequest, byte> _pending = new();

	private readonly ConcurrentQueue<string> _history = new();
	private readonly Process                 _proc = process ?? throw new ArgumentNullException(nameof(process));
	private readonly SemaphoreSlim           _commandLock = new(1, 1);
	private readonly SemaphoreSlim           _stdinWriteLock = new(1, 1);
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
			ContainsAnyToken,
			tokens,
			ct);

		foreach (string line in _history)
		{
			if (req.TryAccept(line))
				return req.Task;
		}

		RegisterPending(req);

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
				ContainsAnyToken,
				until,
				ct);

			RegisterPending(req);

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
				// Ensure no write is in progress before flushing/disposing.
				try
				{
					await _stdinWriteLock.WaitAsync().ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					firstError ??= ex;
				}

				try
				{
					await sw.FlushAsync().ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					firstError ??= ex;
				}
				finally
				{
					try
					{
						_stdinWriteLock.Release();
					}
					catch
					{
						// ignore
					}
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

		// Dispose the stdin write lock to prevent further writes
		try
		{
			_stdinWriteLock.Dispose();
		}
		catch (Exception ex)
		{
			firstError ??= ex;
		}

		// Log (do not throw from Dispose)
		if (firstError is not null) Logger.LogError(firstError, this, LogCategory.UCI);
	}

	public async ValueTask WriteLineAsync(string command, CancellationToken ct = default)
	{
		ThrowIfDisposed();
		Logger.LogInfo($"[INPUT] {command.Bold()}", this, LogCategory.UCI);

		// Serialize writes and only honor cancellation while waiting for the write slot.
		await _stdinWriteLock.WaitAsync(ct).ConfigureAwait(false);

		try
		{
			// Re-read after acquiring the lock to avoid racing with DisposeAsync.
			var sw = Volatile.Read(ref _stdin);

			if (sw is null)
			{
				// Ensure a consistent exception type
				throw new ObjectDisposedException(nameof(UCIEngine));
			}

			// Do not pass the CancellationToken to the StreamWriter operation:
			// Task.WaitAsync would cancel the wait but not the underlying write,
			// leading to overlapping writes and InvalidOperationException.
			await sw.WriteLineAsync(command).ConfigureAwait(false);
		}
		finally
		{
			_stdinWriteLock.Release();
		}
	}

	public void Start()
	{
		ThrowIfDisposed();

		_proc.Start();
		_stdin           = _proc.StandardInput;
		_stdin.AutoFlush = true;

		_ = Task.Run(ReadLoop);
	}

	// Helper: check if a line contains any of the given tokens (case-insensitive).
	private static bool ContainsAnyToken(string line, string[] tokens)
	{
		foreach (string t in tokens)
		{
			if (line.AsSpan().Contains(t.AsSpan(), StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
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

		// If the engine closed stdout and we aren't in normal disposal, fail outstanding waits
		if (!_disposed.IsPositive())
		{
			var ex = new EndOfStreamException("Engine process closed its stdout.");

			try
			{
				_out.Writer.TryComplete(ex);
			}
			catch
			{
				// ignore
			}

			foreach (var req in _pending.Keys)
			{
				if (_pending.TryRemove(req, out _))
				{
					try
					{
						req.Fail(ex);
					}
					catch
					{
						// ignore
					}
				}
			}
		}
	}

	// Helper: register a pending request and ensure it is removed upon completion.
	private void RegisterPending(PendingRequest req)
	{
		_pending.TryAdd(req, 0);
		_ = req.Task.ContinueWith(
			_ => _pending.TryRemove(req, out byte _),
			CancellationToken.None,
			TaskContinuationOptions.ExecuteSynchronously,
			TaskScheduler.Default);
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

			_ctr = ct.Register(() => _tcs.TrySetCanceled(ct));

			_ = _tcs.Task.ContinueWith(
				static (t, state) => ((CancellationTokenRegistration)state!).Dispose(),
				_ctr,
				TaskScheduler.Default);
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
