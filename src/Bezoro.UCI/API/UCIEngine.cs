using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Bezoro.UCI.API
{
	/// <summary>
	///     Handles the low-level communication with the UCI engine process
	/// </summary>
	public class UCIEngine : IAsyncDisposable
	{
		private readonly ConcurrentQueue<string> _incomingLines = new();
		private readonly Process                 _engineProcess;
		private readonly SemaphoreSlim           _lineSignal = new(0);
		private readonly SemaphoreSlim           _streamLock = new(1, 1);
		private volatile bool                    _isDisposed;
		private          StreamReader            _output;
		private          StreamWriter            _input;
		private          Task?                   _readerTask;

		public event EventHandler<string>? InfoReceived;

		public UCIEngine(Process engineProcess)
		{
			_engineProcess = engineProcess ?? throw new ArgumentNullException(nameof(engineProcess));
		}

		public async Task StartAsync()
		{
			if (!_engineProcess.Start())
			{
				throw new InvalidOperationException("Failed to start the UCI engine process.");
			}

			_input  = _engineProcess.StandardInput;
			_output = _engineProcess.StandardOutput;

			// Start the output reader loop
			_readerTask = Task.Run(PumpOutputAsync);
		}

		public async Task WriteLineAsync(string command)
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UCIEngine));
			}

			// Ensure only one write operation at a time
			await _streamLock.WaitAsync().ConfigureAwait(false);
			try
			{
				await _input.WriteLineAsync(command).ConfigureAwait(false);
				await _input.FlushAsync().ConfigureAwait(false);
			}
			finally
			{
				_streamLock.Release();
			}
		}

		public async Task<string?> WaitForTokenAsync(string token, CancellationToken ct = default)
		{
			var endTokens = new[] { "bestmove", "readyok", "uciok" }; // Common UCI end tokens

			while (true)
			{
				string? line = await ReadNextOutputLineAsync(ct);

				if (line.StartsWith("info ", StringComparison.OrdinalIgnoreCase))
				{
					InfoReceived?.Invoke(this, line);
					continue;
				}

				if (line.Contains(token, StringComparison.OrdinalIgnoreCase))
				{
					return line;
				}

				// Check if we've hit a natural end point
				if (endTokens.Any(endToken => line.StartsWith(endToken, StringComparison.OrdinalIgnoreCase)))
				{
					return null; // End of this command's output
				}
			}
		}

		public async Task<string> ReadNextOutputLineAsync(CancellationToken ct)
		{
			await _lineSignal.WaitAsync(ct).ConfigureAwait(false);
			if (_incomingLines.TryDequeue(out string? line))
			{
				Logger.LogInfo($"[OUTPUT] {line}", this, LogCategory.UCI);
				return line;
			}

			// In theory we never get here, but just in case:
			return string.Empty;
		}

		public async ValueTask DisposeAsync()
		{
			if (_isDisposed)
			{
				return;
			}

			_isDisposed = true;

			_readerTask.Dispose();
			_input?.Close();
			_output?.Close();
			_engineProcess?.Dispose();

			_streamLock.Dispose();
			_lineSignal.Dispose();
		}

		private async Task PumpOutputAsync()
		{
			while (!_isDisposed && !_engineProcess.HasExited)
			{
				string? line = await _output.ReadLineAsync().ConfigureAwait(false);
				if (line == null)
				{
					break; // stream closed
				}

				_incomingLines.Enqueue(line);
				_lineSignal.Release();
			}
		}
	}
}