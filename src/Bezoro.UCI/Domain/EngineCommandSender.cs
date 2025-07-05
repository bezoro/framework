using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.Domain.Constants;
using Bezoro.UCI.Domain.Exceptions;

namespace Bezoro.UCI.Domain
{
	/// <summary>
	///     Handles sending commands to the chess engine.
	/// </summary>
	internal sealed class EngineCommandSender
	{
		private readonly EngineProcessManager _processManager;
		private readonly SemaphoreSlim        _commandSemaphore = new(1, 1);

		/// <summary>
		///     Initializes a new instance of the <see cref="EngineCommandSender" /> class.
		/// </summary>
		/// <param name="processManager">The engine process manager.</param>
		public EngineCommandSender(EngineProcessManager processManager)
		{
			_processManager = processManager;
		}

		/// <summary>
		///     Sends a command to the engine and optionally waits for the engine to be ready.
		/// </summary>
		/// <param name="command">The command to send.</param>
		/// <param name="waitForReady">Whether to wait for the engine to be ready after sending the command.</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		public async Task SendCommandAsync(
			string command, bool waitForReady = false, CancellationToken cancellationToken = default)
		{
			await _commandSemaphore.WaitAsync(cancellationToken);
			try
			{
				Logger.LogInfo($"<<UCI>>[{command}] Started.");
				await _processManager.WriteLineAsync(command);

				if (waitForReady)
				{
					await WaitUntilReadyResponseAsync(cancellationToken);
				}
			}
			finally
			{
				_commandSemaphore.Release();
			}

			Logger.LogInfo($"<<UCI>>[{command}] Sent.");
		}

		/// <summary>
		///     Sends the "isready" command and waits for the "readyok" response.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		internal async Task WaitUntilReadyResponseAsync(CancellationToken cancellationToken)
		{
			await _processManager.WriteLineAsync(UCIConstants.IsReadyCommand);
			while (true)
			{
				string? line = await _processManager.ReadLineAsync(cancellationToken);
				if (line == null)
				{
					throw new UCIException("Engine disconnected while waiting for readyok");
				}

				Logger.LogInfo($"<<UCI>>{line}");

				if (line.Equals("readyok", StringComparison.OrdinalIgnoreCase))
				{
					return;
				}
			}
		}
	}
}
