using System.Threading.Tasks;
using Bezoro.UCI.Domain.Exceptions;

namespace Bezoro.UCI.API.Commands
{
	/// <summary>
	///     Command for getting the current FEN from the engine
	/// </summary>
	public readonly record struct GetCurrentFENCommand : IEngineCommand
	{
		public async Task<object> ExecuteAsync(UCIEngine engine)
		{
			Logger.LogInfo("Getting Current FEN...", this, LogCategory.UCI);

			// Request the engine to dump the current position (Stockfish and many UCI engines respond to "d" with a "Fen: ..." line)
			await engine.WriteLineAsync("d");

			// Wait for the line that contains the FEN
			string? fenLine = await engine.WaitForTokenAsync("Fen:");

			const string prefix = "Fen: ";
			if (fenLine.StartsWith(prefix))
			{
				string fen = fenLine.Substring(prefix.Length).Trim();
				Logger.LogSuccess($"Current FEN: {fen}");
				return fen;
			}

			throw new UCIException($"FEN line not found in response: {fenLine}");
		}
	}
}
