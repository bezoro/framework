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
	private const int HISTORY_CAPACITY = 1_000;
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
		ThrowIfDisposed();

		var req = CreatePending(tokens, ct);

		foreach (string line in _history)
		{
			if (req.TryAccept(line))
				return req.Task;
		}

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
			var req = CreatePending(until, ct);

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
		if (Interlocked.Exchange(ref _disposed, 1) == 1)
			return;

		Exception? firstError = null;
		var        disposedEx = new ObjectDisposedException(nameof(UCIEngine));

		try
		{
			_out.Writer.TryComplete(disposedEx);
		}
		catch (Exception ex)
		{
			firstError ??= ex;
		}

		try
		{
			foreach (var kvp in _pending.Keys)
			{
				if (!_pending.TryRemove(kvp, out _)) continue;

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
		catch (Exception ex)
		{
			firstError ??= ex;
		}

		try
		{
			var sw = Interlocked.Exchange(ref _stdin, null);

			if (sw is not null)
			{
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
					await sw.DisposeAsync();
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

		try
		{
			_history.Clear();
		}
		catch (Exception ex)
		{
			firstError ??= ex;
		}

		try
		{
			_commandLock.Dispose();
		}
		catch (Exception ex)
		{
			firstError ??= ex;
		}

		try
		{
			_stdinWriteLock.Dispose();
		}
		catch (Exception ex)
		{
			firstError ??= ex;
		}

		if (firstError is not null)
			Logger.LogError(firstError, this, LogCategory.UCI);
	}

	public async ValueTask WriteLineAsync(string command, CancellationToken ct = default)
	{
		ThrowIfDisposed();


		await _stdinWriteLock.WaitAsync(ct).ConfigureAwait(false);

		try
		{
			var sw = Volatile.Read(ref _stdin);

			if (sw is null) throw new ObjectDisposedException(nameof(UCIEngine));

			Logger.LogInfo($"[INPUT] {command.Bold()}", this, LogCategory.UCI);
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

	private static bool ContainsAnyToken(string line, string[] tokens)
	{
		foreach (string t in tokens)
		{
			if (line.AsSpan().Contains(t.AsSpan(), StringComparison.OrdinalIgnoreCase))
				return true;
		}

		return false;
	}

	private PendingRequest CreatePending(string[] tokens, CancellationToken ct)
		=> RegisterAndReturn(new(ContainsAnyToken, tokens, ct));

	private PendingRequest RegisterAndReturn(PendingRequest req)
	{
		RegisterPending(req);
		return req;
	}

	private async Task ReadLoop()
	{
		using var stdout = _proc.StandardOutput;

		while (!_disposed.IsPositive() && await stdout.ReadLineAsync() is { } line)
		{
			Logger.LogInfo($"[OUTPUT] {line.Bold()}", this, LogCategory.UCI);

			AppendHistory(line);

			_out.Writer.TryWrite(line);

			foreach (var kvp in _pending)
			{
				var req = kvp.Key;
				if (req.TryAccept(line))
					_pending.TryRemove(req, out _);
			}
		}

		if (!_disposed.IsPositive())
		{
			var ex = new EndOfStreamException("Engine process closed its stdout.");
			CompleteOutputAndFailPending(ex);
		}
	}

	private void AppendHistory(string line)
	{
		_history.Enqueue(line);
		if (_history.Count > HISTORY_CAPACITY)
			_history.TryDequeue(out _);
	}

	private void CompleteOutputAndFailPending(Exception ex)
	{
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
			if (!_pending.TryRemove(req, out _)) continue;

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
