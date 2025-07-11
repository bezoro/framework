using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain.Helpers;

namespace Bezoro.UCI.API.Commands
{
	/// <summary>
	///     Command for getting legal moves for a specific square with their classifications
	/// </summary>
	public readonly record struct GetLegalMovesForSquareWithDetailsCommand : IEngineCommand
	{
		private readonly CancellationToken _cancellationToken;
		private readonly string            _square;

		public GetLegalMovesForSquareWithDetailsCommand(string square, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(square))
			{
				throw new ArgumentException("Square cannot be null or whitespace.", nameof(square));
			}

			if (!UCIHelper.IsValidAlgebraicNotation(square))
			{
				throw new ArgumentException($"Square '{square}' is not in valid algebraic notation (e.g. 'e2').",
					nameof(square));
			}

			_square            = square;
			_cancellationToken = cancellationToken;
		}

		public async Task<object> ExecuteAsync(UCIEngine engine)
		{
			// Get all legal moves with details
			var allMovesCommand     = new GetAllLegalMovesWithDetailsCommand(_cancellationToken);
			var allMovesWithDetails = (List<MoveClassification>)await allMovesCommand.ExecuteAsync(engine);

			// Filter moves for the specified square
			string? s = _square;
			List<MoveClassification> movesForSquare = allMovesWithDetails.
													  Where(m => m.Move.StartsWith(s,
														  StringComparison.OrdinalIgnoreCase)).ToList();

			return movesForSquare;
		}
	}
}
