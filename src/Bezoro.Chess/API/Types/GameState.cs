using System;
using System.Collections.Immutable;
using Bezoro.UCI.API.Types;

namespace Bezoro.Chess.API.Types;

/// <summary>
///     Represents the complete state of a chess game at a point in time.
///     Designed for Unity consumption with all data needed for rendering.
/// </summary>
/// <param name="CurrentFen">The current position as FEN.</param>
/// <param name="SideToMove">The color that is to move.</param>
/// <param name="MoveNumber">The current full move number.</param>
/// <param name="PlayedMoves">List of moves played in the game.</param>
/// <param name="LegalMoves">List of legal moves in the current position.</param>
/// <param name="ClassifiedMoves">Dictionary of classified moves for highlighting.</param>
/// <param name="Result">The game result (ongoing, win, draw).</param>
/// <param name="Evaluation">Current engine evaluation, if available.</param>
/// <param name="BestMove">Engine's best move suggestion, if available.</param>
/// <param name="LastMove">The last move played, if any.</param>
/// <param name="IsCheck">Whether the side to move is in check.</param>
/// <param name="CheckSquare">The square of the king in check, for highlighting.</param>
public readonly record struct GameState(
	Fen                                             CurrentFen,
	PlayerColor                                     SideToMove,
	int                                             MoveNumber,
	ImmutableList<ChessMove>                        PlayedMoves,
	ImmutableList<string>                           LegalMoves,
	ImmutableDictionary<string, MoveClassification> ClassifiedMoves,
	GameResult                                      Result,
	int?                                            Evaluation,
	string?                                         BestMove,
	ChessMove?                                      LastMove,
	bool                                            IsCheck,
	string?                                         CheckSquare
)
{
    /// <summary>
    ///     Gets the default initial game state (standard starting position).
    /// </summary>
    public static GameState Default { get; } = new(
		Fen.Default,
		PlayerColor.White,
		1,
		ImmutableList<ChessMove>.Empty,
		ImmutableList<string>.Empty,
		ImmutableDictionary<string, MoveClassification>.Empty,
		GameResult.Ongoing,
		null,
		null,
		null,
		false,
		null
	);

    /// <summary>
    ///     Gets whether it is black's turn to move.
    /// </summary>
    public bool IsBlackToMove => SideToMove == PlayerColor.Black;

    /// <summary>
    ///     Gets whether the position is checkmate.
    /// </summary>
    public bool IsCheckmate => Result.Reason == TerminationReason.Checkmate;

    /// <summary>
    ///     Gets whether all legal moves have been classified.
    /// </summary>
    public bool IsClassificationComplete => ClassifiedMoveCount >= LegalMoveCount;

    /// <summary>
    ///     Gets whether the game is over.
    /// </summary>
    public bool IsGameOver => Result.IsGameOver;

    /// <summary>
    ///     Gets whether the position is stalemate.
    /// </summary>
    public bool IsStalemate => Result.Reason == TerminationReason.Stalemate;

    /// <summary>
    ///     Gets whether it is white's turn to move.
    /// </summary>
    public bool IsWhiteToMove => SideToMove == PlayerColor.White;

    /// <summary>
    ///     Gets the classification progress (0.0 to 1.0).
    /// </summary>
    public double ClassificationProgress =>
		LegalMoveCount == 0 ? 1.0 : (double)ClassifiedMoveCount / LegalMoveCount;

    /// <summary>
    ///     Gets the evaluation bar percentage (0-100) for UI display.
    ///     50 means equal, higher values favor white.
    /// </summary>
    public double EvaluationBarPercent => (NormalizedEvaluation + 1.0) / 2.0 * 100.0;

    /// <summary>
    ///     Gets the normalized evaluation as a value between -1.0 and 1.0 for UI display.
    ///     Uses a sigmoid-like function to normalize centipawn scores.
    /// </summary>
    public double NormalizedEvaluation
	{
		get
		{
			if (!Evaluation.HasValue) return 0.0;

			// Use a sigmoid-like function: 2 / (1 + e^(-cp/400)) - 1
			// This maps centipawns to roughly -1 to +1 range
			int cp = Evaluation.Value;
			return 2.0 / (1.0 + Math.Exp(-cp / 400.0)) - 1.0;
		}
	}

    /// <summary>
    ///     Gets the number of classified moves.
    /// </summary>
    public int ClassifiedMoveCount => ClassifiedMoves.Count;

    /// <summary>
    ///     Gets the evaluation in centipawns from white's perspective.
    ///     Positive values favor white, negative values favor black.
    /// </summary>
    public int EvaluationCentipawns => Evaluation ?? 0;

    /// <summary>
    ///     Gets the number of legal moves available.
    /// </summary>
    public int LegalMoveCount => LegalMoves.Count;

    /// <summary>
    ///     Gets the total number of half-moves (plies) played.
    /// </summary>
    public int PlyCount => PlayedMoves.Count;

    /// <summary>
    ///     Creates a new state from a UCI state.
    /// </summary>
    public static GameState FromUciState(
		UciState                 uciState,
		ImmutableList<ChessMove> playedMoves,
		GameResult               result)
	{
		var sideToMove = uciState.CurrentFen.ActiveColor == 'w' ? PlayerColor.White : PlayerColor.Black;
		var lastMove   = playedMoves.Count > 0 ? playedMoves[^1] : (ChessMove?)null;

		// Convert UCI classified moves to our classification format
		var classifiedMoves = uciState.ClassifiedMoves.ToImmutableDictionary(
			kvp => kvp.Key,
			kvp => GetClassificationFromUciMove(kvp.Value)
		);

		// Determine if in check
		bool isCheck = !string.IsNullOrEmpty(uciState.CurrentFen.Checkers);

		// Find king square if in check
		string? checkSquare = isCheck ? FindKingSquare(uciState.CurrentFen, sideToMove) : null;

		return new(
			uciState.CurrentFen,
			sideToMove,
			uciState.CurrentFen.FullmoveNumber,
			playedMoves,
			uciState.LegalMoves,
			classifiedMoves,
			result,
			uciState.Evaluation?.ScoreCp,
			uciState.BestMove?.Notation,
			lastMove,
			isCheck,
			checkSquare
		);
	}

    /// <summary>
    ///     Gets the classification for a specific move notation, or Normal if not classified.
    /// </summary>
    public MoveClassification GetMoveClassification(string moveNotation) =>
		ClassifiedMoves.TryGetValue(moveNotation, out var classification)
			? classification
			: MoveClassification.Normal;

	private static MoveClassification GetClassificationFromUciMove(Move move)
	{
		var analysis = move.Analysis;

		if (analysis.IsMate) return MoveClassification.Checkmate;
		if (analysis.IsCheck) return MoveClassification.Check;
		if (analysis.IsEnPassant) return MoveClassification.EnPassant;
		if (analysis.IsCapture) return MoveClassification.Capture;
		if (analysis.IsCastling) return MoveClassification.Castling;
		if (analysis.IsPromotion) return MoveClassification.Promotion;

		return MoveClassification.Normal;
	}

	private static string? FindKingSquare(Fen fen, PlayerColor sideToMove)
	{
		string placement = fen.PiecePlacement;
		char   kingChar  = sideToMove == PlayerColor.White ? 'K' : 'k';

		var rank = 8;
		var file = 0;

		foreach (char token in placement)
		{
			switch (token)
			{
				case '/':
					rank--;
					file = 0;
					continue;
				case >= '1' and <= '8':
					file += token - '0';
					continue;
			}

			if (token == kingChar) return $"{(char)('a' + file)}{rank}";

			if (char.IsLetter(token))
				file++;
		}

		return null;
	}
}
