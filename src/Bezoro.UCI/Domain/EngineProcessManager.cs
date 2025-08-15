using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Core.Common.Extensions;
using Bezoro.UCI.Domain.Constants;
using Bezoro.UCI.Domain.Exceptions;
using Bezoro.UCI.Domain.Extensions;

namespace Bezoro.UCI.Domain
{
	/// <summary>
	///     Manages the lifecycle of a chess engine process.
	/// </summary>
	internal sealed class EngineProcessManager : IAsyncDisposable
	{
		private readonly SemaphoreSlim _readSemaphore = new(1, 1);
		private volatile bool          _isDisposed;
		private          Process?      _engineProcess;
		private          StreamReader? _processOutput;

		private StreamWriter? _processInput;

		/// <summary>
		///     Initializes a new instance of the <see cref="EngineProcessManager" /> class.
		/// </summary>
		/// <param name="enginePath">The file path to the UCI engine executable.</param>
		public EngineProcessManager(string enginePath)
		{
			ThrowIfInvalidEnginePath(enginePath);
			CreateEngineProcess(enginePath);
		}

		/// <summary>
		///     Gets the process output stream.
		/// </summary>
		public StreamReader ProcessOutput => GetProcessOutputOrThrow();

		/// <summary>
		///     Gets the process input stream.
		/// </summary>
		public StreamWriter ProcessInput => GetProcessInputOrThrow();

		/// <summary>
		///     Checks if the engine is ready.
		/// </summary>
		public bool IsReady() => _engineProcess is not { HasExited: true } && !_isDisposed;

		/// <summary>
		///     Stops the engine process.
		/// </summary>
		/// <param name="command">The command to send before stopping.</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		public async Task StopAsync(CancellationToken cancellationToken = default)
		{
			ThrowIfInvalidState();

			try
			{
				await _processInput.WriteLineAsync(UciConstants.QUIT_COMMAND);

				// Asynchronously wait for the process to exit with a timeout.
				using var cts       = new CancellationTokenSource(TimeSpan.FromSeconds(5));
				using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);
				await _engineProcess.WaitForExitAsync(linkedCts.Token);
			}
			catch (Exception)
			{
				// If graceful shutdown fails, kill the process.
				if (!_engineProcess.HasExited)
				{
					_engineProcess.Kill();
				}
			}
		}

		/// <summary>
		///     Writes a line to the process input.
		/// </summary>
		/// <param name="command">The command to write.</param>
		public async Task WriteLineAsync(string command)
		{
			ThrowIfDisposed();
			ThrowIfProcessInputIsNull();

			Logger.LogInfo($"[WRITE LINE] -> {command.Bold()}", this, LogCategory.UCI);
			await _processInput.WriteLineAsync(command);
			await _processInput.FlushAsync();
		}

		/// <summary>
		///     Reads a line from the process output.
		/// </summary>
		/// <param name="ct">A token to cancel the operation.</param>
		/// <param name="timeoutMs">The timeout in milliseconds.</param>
		public async Task<string?> ReadLineAsync(CancellationToken ct, int timeoutMs = 5000)
		{
			ThrowIfDisposed();
			ThrowIfProcessOutputIsNull();

			await _readSemaphore.WaitAsync(ct);
			try
			{
				Task<string?> readTask      = _processOutput.ReadLineAsync();
				var           completedTask = await Task.WhenAny(readTask, Task.Delay(timeoutMs, ct));
				if (completedTask != readTask)
				{
					throw new TimeoutException("The engine response timed out.");
				}

				string? result = await readTask;
				Logger.LogInfo($"Line -> {result}", this, LogCategory.UCI);
				return result;
			}
			finally
			{
				_readSemaphore.Release();
			}
		}

		/// <summary>
		///     Disposes the engine process manager.
		/// </summary>
		public async ValueTask DisposeAsync()
		{
			if (_isDisposed)
			{
				return;
			}

			_isDisposed = true;

			if (_processInput != null)
			{
				_processInput.Dispose();
				_processInput = null;
			}

			if (_processOutput != null)
			{
				_processOutput.Dispose();
				_processOutput = null;
			}

			if (!_engineProcess.HasExited)
			{
				try
				{
					_engineProcess.Kill();
					await _engineProcess.WaitForExitAsync();
				}
				catch
				{
					// Ignore exceptions during cleanup
				}
			}

			_engineProcess.Dispose();
			GC.SuppressFinalize(this);
		}

		/// <summary>
		///     Starts the engine process.
		/// </summary>
		public void StartEngine()
		{
			ThrowIfDisposed();

			_engineProcess?.Start();
			_processInput  = _engineProcess?.StandardInput;
			_processOutput = _engineProcess?.StandardOutput;
		}

		private static void ThrowIfInvalidEnginePath(string enginePath)
		{
			if (string.IsNullOrWhiteSpace(enginePath))
			{
				throw new ArgumentException("Engine path cannot be null or whitespace.", nameof(enginePath));
			}
		}

		private StreamReader GetProcessOutputOrThrow()
		{
			ThrowIfDisposed();
			ThrowIfProcessOutputIsNull();
			return _processOutput;
		}

		private StreamWriter GetProcessInputOrThrow()
		{
			ThrowIfDisposed();
			ThrowIfProcessInputIsNull();
			return _processInput;
		}

		private void CreateEngineProcess(string enginePath)
		{
			_engineProcess = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName               = enginePath,
					UseShellExecute        = false,
					RedirectStandardInput  = true,
					RedirectStandardOutput = true,
					CreateNoWindow         = true
				},
				EnableRaisingEvents = true
			};
		}

		private void ThrowIfDisposed()
		{
			if (_isDisposed)
			{
				throw new ObjectDisposedException(nameof(EngineProcessManager),
					$"{nameof(EngineProcessManager)} has been disposed.");
			}
		}

		private void ThrowIfEngineIsNull()
		{
			if (_engineProcess == null)
			{
				throw new UCIException("Engine process is null.");
			}
		}

		private void ThrowIfInvalidState()
		{
			ThrowIfDisposed();
			ThrowIfEngineIsNull();
			ThrowIfProcessInputIsNull();
			ThrowIfProcessOutputIsNull();
		}

		private void ThrowIfProcessInputIsNull()
		{
			if (_processInput == null)
			{
				throw new UCIException("Engine process input stream is null. Ensure the engine is started.");
			}
		}

		private void ThrowIfProcessOutputIsNull()
		{
			if (_processOutput == null)
			{
				throw new UCIException("Engine process output stream is null.");
			}
		}
	}
}
