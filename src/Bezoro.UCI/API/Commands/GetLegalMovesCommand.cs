using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.Domain.Constants;

namespace Bezoro.UCI.API.Commands
{
	/// <summary>
	///     Command for getting all legal moves from the engine
	/// </summary>
	public readonly record struct GetLegalMovesCommand : IEngineCommand
	{
		private readonly CancellationToken _cancellationToken;

		public GetLegalMovesCommand(CancellationToken cancellationToken = default)
		{
			_cancellationToken = cancellationToken;
		}

		public async Task<object> ExecuteAsync(UCIEngine engine)
		{
			var moves = new List<string>();

			// Fire off the perft command
			await engine.WriteLineAsync(UCIConstants.GoPerftDepth1Command);

			while (true)
			{
				// Wait for the next line from the engine
				string line = await engine.ReadNextOutputLineAsync(_cancellationToken);

				// Once we hit the summary line, stop
				if (line.Contains("Nodes searched", StringComparison.OrdinalIgnoreCase))
				{
					break;
				}

				// Otherwise try to parse a move out of it
				var match = UCIConstants.MoveRegex.Match(line);
				if (match.Success)
				{
					moves.Add(match.Groups[1].Value);
				}
			}

			return moves;
		}
	}
}
