using System.Collections.Generic;
using System.Collections.Immutable;
using Bezoro.Chess.UCI.Protocol.Internal;

namespace Bezoro.Chess.UCI.Protocol.API.Common.Extensions;

/// <summary>
///     Extension methods for deriving structural move classifications directly from a FEN and UCI move strings.
/// </summary>
public static class FenMoveClassificationExtensions
{
	/// <summary>
	///     Classifies a set of legal moves structurally without engine search.
	/// </summary>
	/// <param name="fen">Current position.</param>
	/// <param name="legalMoves">Legal moves in lowercase UCI notation.</param>
	/// <returns>Move classifications keyed by move notation.</returns>
	public static ImmutableDictionary<string, MoveClassification> ClassifyMoves(
		this Fen            fen,
		IEnumerable<string> legalMoves) =>
		LocalPositionRules.ClassifyMoves(fen, legalMoves);

	/// <summary>
	///     Classifies a set of legal moves structurally and resolves check, mate, and stalemate locally without engine search.
	/// </summary>
	/// <param name="fen">Current position.</param>
	/// <param name="legalMoves">Legal moves in lowercase UCI notation.</param>
	/// <returns>Fully resolved move classifications keyed by move notation.</returns>
	public static ImmutableDictionary<string, MoveClassification> ClassifyMovesFully(
		this Fen            fen,
		IEnumerable<string> legalMoves) =>
		LocalPositionRules.ClassifyMovesFully(fen, legalMoves);

	/// <summary>
	///     Classifies a single legal move structurally without engine search.
	/// </summary>
	/// <param name="fen">Current position.</param>
	/// <param name="move">Move in lowercase UCI notation.</param>
	/// <returns>Structural move classification with unresolved tactical flags.</returns>
	public static MoveClassification ClassifyMove(this Fen fen, string move) =>
		LocalPositionRules.ClassifyMove(fen, move);

	/// <summary>
	///     Classifies a single legal move structurally and resolves check, mate, and stalemate locally without engine search.
	/// </summary>
	/// <param name="fen">Current position.</param>
	/// <param name="move">Move in lowercase UCI notation.</param>
	/// <returns>Fully resolved move classification.</returns>
	public static MoveClassification ClassifyMoveFully(this Fen fen, string move) =>
		LocalPositionRules.ClassifyMoveFully(fen, move);
}
