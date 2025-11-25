using System.Collections.Immutable;

namespace Bezoro.UCI.API.Types;

/// <summary>
///     Represents an immutable snapshot of the UCI Coordinator's state.
/// </summary>
public record UciState(
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
}
