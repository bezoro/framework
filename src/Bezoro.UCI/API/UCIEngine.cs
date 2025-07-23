using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
		private readonly SemaphoreSlim _ioLock = new(1, 1);

		private volatile bool _isDisposed;

		private StreamReader? _output;
		private StreamWriter? _input;

		public UCIEngine(Process engineProcess)
		{
			_engineProcess = engineProcess ?? throw new ArgumentNullException(nameof(engineProcess));
		}

		public async Task WriteLineAsync(string command, CancellationToken ct = default)
		{
			ThrowIfDisposed();
			await _ioLock.WaitAsync(ct);
			try
			{
				await _input.WriteLineAsync(command);
				Logger.LogInfo($"[INPUT] {command.Bold()}", this, LogCategory.UCI);
				await _input.FlushAsync();
			}
			finally
			{
				_ioLock.Release();
			}
		}

		public async Task<List<string>> SendCommandAndReadOutputAsync(
			string command,
			CancellationToken ct = default)
		{
			string[] terminators = [ "bestmove", "uciok", "readyok", "nodes searched", "checkers" ];

			await _ioLock.WaitAsync(ct);
			try
			{
				await _input.WriteLineAsync(command);
				Logger.LogInfo($"[INPUT] {command.Bold()}", this, LogCategory.UCI);
				await _input.FlushAsync();

				IReadOnlyList<string> lines = await ReadUntilAsync(
					l => terminators.Any(t => l.Contains(t, StringComparison.OrdinalIgnoreCase)), ct);

				return [ ..lines ];
			}
			finally
			{
				_ioLock.Release();
			}
		}

		public async Task<string> WaitForTokenAsync(
			string token,
			CancellationToken ct = default)
		{
			if (_isDisposed) return string.Empty;

			await _ioLock.WaitAsync(ct);
			try
			{
				Logger.LogInfo($"Waiting for: {token.Bold()}", this, LogCategory.UCI);

				IReadOnlyList<string> lines =
					await ReadUntilAsync(l => l.Contains(token, StringComparison.OrdinalIgnoreCase), ct);

				return lines.LastOrDefault(l => l.Contains(token, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
			}
			finally
			{
				_ioLock.Release();
			}
		}

		public async ValueTask DisposeAsync()
		{
			if (_isDisposed)
			{
				return;
			}

			_ioLock.Dispose();
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

		/// <summary>
		///     Core routine: reads lines until <paramref name="stopCondition" /> returns true,
		///     the engine is disposed, EOF is reached or the operation is cancelled.
		/// </summary>
		private async Task<IReadOnlyList<string>> ReadUntilAsync(Func<string, bool> stopCondition, CancellationToken ct)
		{
			var lines = new List<string>();

			while (!ct.IsCancellationRequested && !_isDisposed)
			{
				string? line = await _output.ReadLineAsync();
				if (line is null) break;

				Logger.LogInfo($"[OUTPUT] {line.Bold()}");
				lines.Add(line);

				if (stopCondition(line))
				{
					Logger.LogInfo($"Stop-condition satisfied by -> {line.Bold()}", this, LogCategory.UCI);

					break;
				}
			}

			return lines;
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
