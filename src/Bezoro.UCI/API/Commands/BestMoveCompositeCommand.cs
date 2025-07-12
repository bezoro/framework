using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Bezoro.UCI.API.Commands
{
	/// <summary>
	///     A composite command for getting the best move from the engine
	/// </summary>
	public class BestMoveCompositeCommand : IEngineCommand<(string best, string ponder)?>
	{
		private readonly int _depth;
		private static readonly Regex BestMoveRegex =
			new(@"bestmove\s+([a-h][1-8][a-h][1-8][qrbn]?)(?:\s+ponder\s+([a-h][1-8][a-h][1-8][qrbn]?))?");

		/// <summary>
		///     Creates a new best move command
		/// </summary>
		/// <param name="depth">The search depth</param>
		public BestMoveCompositeCommand(int depth)
		{
			_depth = depth;
		}

		/// <summary>
		///     Executes the command
		/// </summary>
		/// <param name="engine">The UCI engine</param>
		/// <returns>A tuple with the best move and ponder move</returns>
		public async Task<(string best, string ponder)?> ExecuteAsync(UCIEngine engine)
		{
			// Build the command sequence internally
			var sendCommand = new SendTextCommand($"go depth {_depth}");
			var waitCommand = new WaitForTokenCommand("bestmove");

			// Execute the commands
			await sendCommand.ExecuteAsync(engine).ConfigureAwait(false);
			string result = await waitCommand.ExecuteAsync(engine).ConfigureAwait(false);

			// Parse the result
			var match = BestMoveRegex.Match(result);
			if (match.Success)
			{
				string bestMove = match.Groups[1].Value;
				string ponderMove = match.Groups.Count > 2 && match.Groups[2].Success
					? match.Groups[2].Value
					: string.Empty;

				return (bestMove, ponderMove);
			}

			return (string.Empty, string.Empty);
		}
	}
}
