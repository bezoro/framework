using Bezoro.UCI.API.Types;

namespace Bezoro.Chess.API.Types;

/// <summary>
///     Classification of a chess move for UI highlighting purposes.
/// </summary>
public enum MoveClassification
{
	/// <summary>A regular quiet move.</summary>
	Normal,

	/// <summary>A capturing move (highlight in red).</summary>
	Capture,

	/// <summary>An en passant capture.</summary>
	EnPassant,

	/// <summary>A castling move (kingside or queenside).</summary>
	Castling,

	/// <summary>A pawn promotion.</summary>
	Promotion,

	/// <summary>A move that gives check.</summary>
	Check,

	/// <summary>A move that delivers checkmate.</summary>
	Checkmate
}

/// <summary>
///     Represents a chess move with classification and display information.
///     Designed for Unity consumption to render moves with proper highlighting.
/// </summary>
/// <param name="Notation">The move in UCI notation (e.g., "e2e4").</param>
/// <param name="FromSquare">The source square (e.g., "e2").</param>
/// <param name="ToSquare">The destination square (e.g., "e4").</param>
/// <param name="MovingColor">The color that played the move.</param>
/// <param name="Classification">The move classification for highlighting.</param>
/// <param name="ScoreCentipawns">The centipawn evaluation after this move, if available.</param>
/// <param name="ScoreMate">Mate-in-N score, if this move leads to forced mate.</param>
/// <param name="SanNotation">Standard algebraic notation (e.g., "e4"), if available.</param>
public readonly record struct ChessMove(
	string             Notation,
	string             FromSquare,
	string             ToSquare,
	PlayerColor        MovingColor,
	MoveClassification Classification,
	int?               ScoreCentipawns = null,
	int?               ScoreMate       = null,
	string?            SanNotation     = null
)
{
    /// <summary>
    ///     Gets whether this move is a capture.
    /// </summary>
    public bool IsCapture => Classification is MoveClassification.Capture or MoveClassification.EnPassant;

    /// <summary>
    ///     Gets whether this move gives check.
    /// </summary>
    public bool IsCheck => Classification == MoveClassification.Check;

    /// <summary>
    ///     Gets whether this move delivers checkmate.
    /// </summary>
    public bool IsCheckmate => Classification == MoveClassification.Checkmate;

    /// <summary>
    ///     Gets the file index (0-7) of the source square.
    /// </summary>
    public int FromFile => FromSquare.Length > 0 ? FromSquare[0] - 'a' : 0;

    /// <summary>
    ///     Gets the rank index (0-7) of the source square.
    /// </summary>
    public int FromRank => FromSquare.Length > 1 ? FromSquare[1] - '1' : 0;

    /// <summary>
    ///     Gets the file index (0-7) of the destination square.
    /// </summary>
    public int ToFile => ToSquare.Length > 0 ? ToSquare[0] - 'a' : 0;

    /// <summary>
    ///     Gets the rank index (0-7) of the destination square.
    /// </summary>
    public int ToRank => ToSquare.Length > 1 ? ToSquare[1] - '1' : 0;

    /// <summary>
    ///     Creates a ChessMove from move notation and optional classification.
    /// </summary>
    public static ChessMove FromNotation(
		string              notation,
		PlayerColor         movingColor,
		MoveClassification? classification = null)
	{
		var parsed = ParsedMove.FromNotation(notation);
		return new(
			parsed.Notation,
			parsed.From,
			parsed.To,
			movingColor,
			classification ?? MoveClassification.Normal
		);
	}

    /// <summary>
    ///     Creates a ChessMove from a UCI Move and side to move.
    /// </summary>
    public static ChessMove FromUciMove(Move uciMove, PlayerColor movingColor) => new(
		uciMove.Notation,
		uciMove.From,
		uciMove.To,
		movingColor,
		ClassifyMove(uciMove),
		uciMove.Analysis.Score.ScoreCp,
		uciMove.Analysis.Score.ScoreMate
	);

	private static MoveClassification ClassifyMove(Move uciMove)
	{
		var analysis = uciMove.Analysis;

		if (analysis.IsMate) return MoveClassification.Checkmate;
		if (analysis.IsCheck) return MoveClassification.Check;
		if (analysis.IsEnPassant) return MoveClassification.EnPassant;
		if (analysis.IsCapture) return MoveClassification.Capture;
		if (analysis.IsCastling) return MoveClassification.Castling;
		if (analysis.IsPromotion) return MoveClassification.Promotion;

		return MoveClassification.Normal;
	}
}
