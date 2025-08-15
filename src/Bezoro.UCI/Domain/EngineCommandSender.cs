using System.Threading;
using System.Threading.Tasks;
using Bezoro.Core.Common.Extensions;
using Bezoro.UCI.Domain.Common.Constants;
using Bezoro.UCI.Domain.Common.Exceptions;

namespace Bezoro.UCI.Domain;

/// <summary>
///     Handles sending commands to the chess engine.
/// </summary>
internal sealed class EngineCommandSender
{
	private readonly EngineOutputParser   _outputParser;
	private readonly EngineProcessManager _processManager;
	private readonly SemaphoreSlim        _commandSemaphore = new(1, 1);

	/// <summary>
	///     Initializes a new instance of the <see cref="EngineCommandSender" /> class.
	/// </summary>
	/// <param name="processManager">The engine process manager.</param>
	public EngineCommandSender(EngineProcessManager processManager, EngineOutputParser outputParser)
	{
		_processManager = processManager;
		_outputParser   = outputParser;
	}

	/// <summary>
	///     Sends a command to the engine and optionally waits for the engine to be ready.
	/// </summary>
	/// <param name="command">The command to send.</param>
	/// <param name="waitForReady">Whether to wait for the engine to be ready after sending the command.</param>
	/// <param name="ct">A token to cancel the operation.</param>
	public async Task SendCommandAsync(
		string            command,
		bool              waitForReady = false,
		CancellationToken ct           = default)
	{
		Logger.LogInfo($"[COMMAND] {command.Bold()} Started.", this, LogCategory.UCI);
		await _commandSemaphore.WaitAsync(ct);
		try
		{
			await _processManager.WriteLineAsync(command);

			if (waitForReady) await WaitUntilReadyResponseAsync(ct);
		}
		finally
		{
			_commandSemaphore.Release();
		}

		Logger.LogInfo($"[COMMAND] {command.Bold()} Finished.", this, LogCategory.UCI);
	}

	/// <summary>
	///     Sends the "isready" command and waits for the "readyok" response.
	/// </summary>
	/// <param name="ct">A token to cancel the operation.</param>
	internal async Task WaitUntilReadyResponseAsync(CancellationToken ct)
	{
		Logger.LogInfo("[COMMAND] Waiting for readyok response.", this, LogCategory.UCI);
		await _processManager.WriteLineAsync(UciConstants.IS_READY_COMMAND);
		while (true)
		{
			string? line = await _outputParser.ReadLineFromProcessAsync(ct);
			if (line == null) throw new UCIException("Engine disconnected while waiting for readyok");

			if (line.Equals("readyok", StringComparison.OrdinalIgnoreCase)) return;
		}
	}
}
