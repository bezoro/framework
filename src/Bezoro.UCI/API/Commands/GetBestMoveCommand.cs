using System.Threading.Tasks;

namespace Bezoro.UCI.API.Commands
{
	/// <summary>
	///     Command for getting the best move from the engine
	/// </summary>
	public readonly record struct GetBestMoveCommand : IEngineCommand
	{
		private readonly int _depth;

		public GetBestMoveCommand(int depth = 20)
		{
			_depth = depth;
		}

		public async Task<object> ExecuteAsync(UCIEngine engine)
		{
			await engine.WriteLineAsync($"go depth {_depth}");
			string line = await engine.WaitForTokenAsync("bestmove");

			// Split on spaces, ignore any extra whitespace
			string[]? tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			// tokens[0] == "bestmove"
			string best = tokens.Length > 1 ? tokens[1] : string.Empty;
			// tokens[2] == "ponder", tokens[3] == the ponder move (if present)
			string ponder = tokens.Length > 3 ? tokens[3] : string.Empty;

			return (best, ponder);
		}
	}
}
