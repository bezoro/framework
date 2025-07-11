using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain.Helpers;

namespace Bezoro.UCI.API.Commands
{
	/// <summary>
	///     Command for getting all legal moves with their classifications
	/// </summary>
	public readonly record struct GetAllLegalMovesWithDetailsCommand : IEngineCommand
	{
		private readonly CancellationToken _cancellationToken;

		public GetAllLegalMovesWithDetailsCommand(CancellationToken cancellationToken = default)
		{
			_cancellationToken = cancellationToken;
		}

		public async Task<object> ExecuteAsync(UCIEngine engine)
		{
			Logger.LogInfo("GettingAllLegalMoves...", this, LogCategory.UCI);

			// Get current FEN
			var fenCommand = new GetCurrentFENCommand();
			var currentFen = (string)await fenCommand.ExecuteAsync(engine);

			// Get legal moves
			var movesCommand = new GetLegalMovesCommand(_cancellationToken);
			var legalMoves   = (List<string>)await movesCommand.ExecuteAsync(engine);

			// Classify moves
			var boardState = BoardStateParser.ParseFen(currentFen);
			List<MoveClassification> classifiedMoves =
				legalMoves.Select(move => MoveClassifier.ClassifyMove(move, boardState)).ToList();

			Logger.LogInfo($"Legal Moves -> {classifiedMoves}", this, LogCategory.UCI);
			Logger.LogInfo("GettingAllLegalMoves...Done",       this, LogCategory.UCI);

			return classifiedMoves;
		}
	}
}
