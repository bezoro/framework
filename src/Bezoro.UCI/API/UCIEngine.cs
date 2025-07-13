using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Core.Common.Extensions;

namespace Bezoro.UCI.API
{
	/// <summary>
	///     Handles the low-level communication with the UCI engine process
	/// </summary>
	public class UCIEngine : IAsyncDisposable
	{
		private readonly Process       _engineProcess;
		private          bool          _isBusy;
		private volatile bool          _isDisposed;
		private          StreamReader? _output;
		private          StreamWriter? _input;

		public event EventHandler<string>? InfoReceived;

		public UCIEngine(Process engineProcess)
		{
			_engineProcess = engineProcess ?? throw new ArgumentNullException(nameof(engineProcess));
		}

		public async Task WriteLineAsync(string command, CancellationToken ct = default)
		{
			ThrowIfDisposed();
			while (_isBusy)
			{
				await Task.Delay(100, ct);
			}

			_isBusy = true;
			await _input.WriteLineAsync(command);
			Logger.LogInfo($"[INPUT] {command.Bold()}", this, LogCategory.UCI);
			await _input.FlushAsync();
			_isBusy = false;
		}

		/// <summary>
		///     Sends a command to the UCI engine and reads all output lines until a termination token is found
		/// </summary>
		/// <param name="command">The command to send to the engine</param>
		/// <param name="terminationTokens">Tokens that indicate end of output (defaults to UCI protocol tokens)</param>
		/// <param name="ct">Cancellation token</param>
		/// <returns>A list of output lines related to the command</returns>
		public async Task<List<string>> SendCommandAndReadOutputAsync(
			string command, CancellationToken ct = default)
		{
			string[] terminationTokens = [ "bestmove", "uciok", "readyok", "nodes searched" ];
			var      lines             = new List<string>();

			await WriteLineAsync(command, ct);

			_isBusy = true;

			Logger.LogInfo($"Waiting for: {command.Bold()}", this, LogCategory.UCI);

			while (true)
			{
				if (_isDisposed)
				{
					return lines;
				}

				string? line = await _output.ReadLineAsync();
				lines.Add(line);

				foreach (string terminationToken in terminationTokens)
				{
					if (!line.Contains(terminationToken, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					_isBusy = false;
					Logger.LogInfo($"Received: {line.Bold()}", this, LogCategory.UCI);
					return lines;
				}
			}
		}

		public async Task<string> WaitForTokenAsync(string token, CancellationToken ct = default)
		{
			ThrowIfDisposed();
			while (_isBusy)
			{
				await Task.Delay(100, ct);
			}

			_isBusy = true;
			Logger.LogInfo($"Waiting for: {token.Bold()}", this, LogCategory.UCI);
			while (true)
			{
				if (_isDisposed)
				{
					return "";
				}

				string? result = await _output.ReadLineAsync();
				if (!result.Contains(token, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				Logger.LogInfo($"Received: {result.Bold()}", this, LogCategory.UCI);
				_isBusy = false;
				return result;
			}
		}

		public async ValueTask DisposeAsync()
		{
			if (_isDisposed)
			{
				return;
			}

			_isDisposed = true;
			_engineProcess.Dispose();
			_input?.Dispose();
			_output?.Dispose();
			GC.SuppressFinalize(this);
		}

		public void Start()
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UCIEngine));
			}

			if (!_engineProcess.Start())
			{
				throw new InvalidOperationException("Failed to start the UCI engine process.");
			}

			_input  = _engineProcess.StandardInput;
			_output = _engineProcess.StandardOutput;
		}

		private void ThrowIfDisposed()
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(UCIEngine));
			}
		}
	}
}
