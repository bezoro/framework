using Bezoro.Core.Common.Extensions;
using Bezoro.UCI.API.Common.Enums;

namespace Bezoro.UCI.API.Types;

/// <summary>
///     Represents a legal chess move and its specific characteristics,
///     such as whether it's a capture, castling, or promotion, etc...
/// </summary>
public readonly record struct MoveAnalysis
{
	public bool      IsCapture   { get; private init; }
	public bool      IsCastling  { get; private init; }
	public bool      IsCheck     { get; private init; }
	public bool      IsEnPassant { get; private init; }
	public bool      IsMate      { get; private init; }
	public bool      IsNormal    { get; private init; }
	public bool      IsPromotion { get; private init; }
	public bool      IsStalemate { get; private init; }
	public MoveScore Score       { get; private init; }
	public Piece?    MovingPiece { get; private init; }


	/// <summary>
	///     Synchronous analyzer used when an engine-derived score is available.
	///     Computes structural move characteristics and sets mate/check from the score.
	/// </summary>
	internal static MoveAnalysis Analyze(string moveNotation, BoardState boardState, MoveScore score, bool isStalemate)
	{
		moveNotation.ThrowIfNull();
		boardState.ThrowIfNull();

		var parsedMove = ParsedMove.FromNotation(moveNotation);

		if (!boardState.TryGetPieceAt(parsedMove.From, out var movingPiece) || movingPiece is null)
			throw new ArgumentException(
				$"No piece found on square '{parsedMove.From}' for move '{moveNotation}'.",
				nameof(boardState));

		bool isCaptureOnToSquare = boardState.TryGetPieceAt(parsedMove.To, out var targetPiece) && targetPiece.HasValue;

		bool isCapture   = false,
			 isCastling  = false,
			 isCheck     = false,
			 isEnpassant = false,
			 isMate      = false,
			 isNormal    = false,
			 isPromotion = false;

		if (CheckIsCastling(parsedMove, movingPiece, boardState))
			isCastling = true;

		if (CheckIsEnPassant(parsedMove, movingPiece.Value, isCaptureOnToSquare, boardState))
		{
			isCapture   = true;
			isEnpassant = true;
		}

		if (CheckIsPromotion(parsedMove, movingPiece.Value)) isPromotion = true;

		if (isCaptureOnToSquare)
			isCapture = true;

		isNormal = !isCapture && !isCastling && !isEnpassant && !isPromotion;

		// Derive mate/check from the engine score:
		// In UCI, a negative mate score from the side to move indicates that side is getting mated.
		// After our move, opponent is to move; mate in 1 for us is therefore -1.
		if (score.ScoreMate.HasValue)
		{
			isMate  = score.ScoreMate.Value == -1;
			isCheck = isMate;
		}

		return new()
		{
			IsCapture   = isCapture,
			IsCastling  = isCastling,
			IsCheck     = isCheck,
			IsEnPassant = isEnpassant,
			IsMate      = isMate,
			IsNormal    = isNormal,
			IsPromotion = isPromotion,
			IsStalemate = isStalemate,
			Score       = score,
			MovingPiece = movingPiece
		};
	}

	private static bool CheckIsCastling(ParsedMove move, Piece? movingPiece, BoardState boardState)
	{
		move.ThrowIfNull();
		boardState.ThrowIfNull();

		if (!movingPiece.HasValue) return false;
		if (char.ToLower(movingPiece.Value.Char) != 'k' || Math.Abs(move.From[0] - move.To[0]) != 2) return false;

		bool isKingside = move.To[0] == 'g';
		return boardState.ActiveColor switch
		{
			PieceColor.White => isKingside
									? boardState.Fen.CastlingRights.Contains('K')
									: boardState.Fen.CastlingRights.Contains('Q'),
			PieceColor.Black => isKingside
									? boardState.Fen.CastlingRights.Contains('k')
									: boardState.Fen.CastlingRights.Contains('q'),
			_ => false
		};
	}

	private static bool CheckIsEnPassant(ParsedMove move, Piece movingPiece, bool isCapture, BoardState boardState)
	{
		move.ThrowIfNull();
		movingPiece.ThrowIfNull();
		boardState.ThrowIfNull();

		return char.ToLower(movingPiece.Char) == 'p' &&
			   move.From[0] != move.To[0] &&
			   !isCapture &&
			   move.To.Equals(boardState.Fen.EnPassantTarget, StringComparison.OrdinalIgnoreCase);
	}

	private static bool CheckIsPromotion(ParsedMove move, Piece movingPiece)
	{
		if (char.ToLower(movingPiece.Char) != 'p') return false;

		char toRank      = move.To[1];
		bool isWhitePawn = char.IsUpper(movingPiece.Char);
		return isWhitePawn && toRank == '8' || !isWhitePawn && toRank == '1';
	}
}
