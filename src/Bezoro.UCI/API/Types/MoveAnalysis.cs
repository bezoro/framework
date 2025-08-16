using System.Threading;
using System.Threading.Tasks;
using Bezoro.Core.Common.Extensions;
using Bezoro.UCI.API.Common.Enums;
using Bezoro.UCI.Domain;

namespace Bezoro.UCI.API.Types;

/// <summary>
///     Represents a legal chess move and its specific characteristics,
///     such as whether it's a capture, castling, or promotion, etc...
/// </summary>
public readonly record struct MoveAnalysis
{
	public bool IsCapture   { get; private init; }
	public bool IsCastling  { get; private init; }
	public bool IsCheck     { get; private init; }
	public bool IsEnPassant { get; private init; }
	public bool IsMate      { get; private init; }
	public bool IsNormal    { get; private init; }
	public bool IsPromotion { get; private init; }
	public bool IsStalemate { get; private init; }
	public int? CpScore     { get; private init; }
	public int? MateScore   { get; private init; }


	/// <summary>
	///     Async factory: computes move characteristics and, if needed, awaits engine evaluation.
	///     This is the single entry point to create a populated MoveAnalysis.
	/// </summary>
	internal static async Task<MoveAnalysis> AnalyzeAsync(string moveNotation, BoardState boardState, UciEngine engine)
	{
		moveNotation.ThrowIfNull();
		boardState.ThrowIfNull();
		engine.ThrowIfNull();

		var parsedMove = ParsedMove.FromNotation(moveNotation);

		boardState.TryGetPieceAt(parsedMove.From, out var movingPiece);

		bool isCaptureOnToSquare = boardState.TryGetPieceAt(parsedMove.To, out var targetPiece) && targetPiece.HasValue;

		bool isCapture   = false,
			 isCastling  = false,
			 isCheck     = false,
			 isEnpassant = false,
			 isMate      = false,
			 isNormal    = false,
			 isPromotion = false;

		if (CheckIsCastling(parsedMove, boardState))
			isCastling = true;

		if (movingPiece.HasValue && CheckIsEnPassant(parsedMove, movingPiece.Value, isCaptureOnToSquare, boardState))
		{
			isCapture   = true;
			isEnpassant = true;
		}

		if (movingPiece.HasValue && CheckIsPromotion(parsedMove, movingPiece.Value)) isPromotion = true;

		if (isCaptureOnToSquare)
			isCapture = true;

		isNormal = !isCapture && !isCastling && !isEnpassant && !isPromotion;

		var score = engine.TryGetMoveScoreFromHistory(parsedMove.Notation) ??
					await engine.CalculateScoreForMoveAsync(parsedMove.Notation, CancellationToken.None)
								.ConfigureAwait(false);

		if (score.ScoreMate.HasValue)
			isCheck = score.ScoreMate.Value == 0;

		bool isStalemante = await engine.WouldMoveLeadToStalemateAsync(parsedMove.Notation);

		return new()
		{
			IsCapture   = isCapture,
			IsCastling  = isCastling,
			IsCheck     = isCheck,
			IsEnPassant = isEnpassant,
			IsMate      = isMate,
			IsNormal    = isNormal,
			IsPromotion = isPromotion,
			IsStalemate = isStalemante,
			CpScore     = score.ScoreCp,
			MateScore   = score.ScoreMate
		};
	}


	private static bool CheckIsCastling(ParsedMove move, BoardState boardState)
	{
		move.ThrowIfNull();
		boardState.ThrowIfNull();

		move.MovingPiece.ThrowIfNull();
		if (char.ToLower(move.MovingPiece.Char) != 'k' || Math.Abs(move.From[0] - move.To[0]) != 2) return false;

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

		return char.ToLower(movingPiece.Char) == 'p'        &&
			   move.From[0]                   != move.To[0] &&
			   !isCapture                                   &&
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
