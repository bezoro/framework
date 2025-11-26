using System.Collections.Immutable;

namespace Bezoro.UCI.API.Types;

/// <summary>
///     Represents an immutable snapshot of the UCI Coordinator's state.
/// </summary>
public readonly record struct UciState(
	Fen                               Fen,
	ImmutableList<string>             PlayedMoves,
	ImmutableList<string>             LegalMoves,
	ImmutableDictionary<string, Move> ClassifiedMoves,
	ParsedMove?                       BestMove,
	ParsedMove?                       PonderMove,
	PrincipalVariation?               Evaluation,
	bool                              IsSearching
)
{
	/// <summary>
	///     Gets the default initial state.
	/// </summary>
	public static UciState Default { get; } = new(
		Fen.Default,
		ImmutableList<string>.Empty,
		ImmutableList<string>.Empty,
		ImmutableDictionary<string, Move>.Empty,
		null,
		null,
		null,
		false
	);

	/// <summary>
	///     Gets the total number of legal moves available for classification.
	/// </summary>
	public int TotalLegalMoves => LegalMoves.Count;

	/// <summary>
	///     Gets the number of moves that have been classified.
	/// </summary>
	public int ClassifiedMovesCount => ClassifiedMoves.Count;

	/// <summary>
	///     Gets the classification progress as a value between 0.0 and 1.0.
	///     Returns 1.0 if there are no legal moves.
	/// </summary>
	public double ClassificationProgress =>
		TotalLegalMoves == 0 ? 1.0 : (double)ClassifiedMovesCount / TotalLegalMoves;

	/// <summary>
	///     Gets a value indicating whether all legal moves have been classified.
	/// </summary>
	public bool IsClassificationComplete => ClassifiedMovesCount >= TotalLegalMoves;
}
