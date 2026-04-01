using System.Collections.Immutable;
using System.Linq;
using Bezoro.Chess.UCI.API;
using Bezoro.Chess.UCI.API.Common.Enums;
using Bezoro.Chess.UCI.API.Types;

namespace Bezoro.Chess.UCI.Internal;

internal static class CoordinatorMoveFactory
{
	public static GameMoveEvent BuildGameMoveEvent(
		Guid          gameId,
		long          moveId,
		UciState      previousState,
		UciState      currentState,
		string        notation,
		Move          move,
		GameMoveActor actor)
	{
		var boardState = BoardState.FromFen(previousState.CurrentFen);
		var parsedMove = ParsedMove.FromNotation(notation);
		var kindFlags = BuildMoveKindFlags(parsedMove, move.Analysis, move.Piece);
		var capturedPiece = ResolveCapturedPiece(boardState, parsedMove, move);
		var secondaryMove = ResolveSecondaryPieceMove(boardState, parsedMove, move);
		var promotionPiece = parsedMove.PromotionPiece;

		return new(
			gameId,
			moveId,
			currentState.PlayedMoves.Count,
			actor,
			notation,
			parsedMove.From,
			parsedMove.To,
			kindFlags,
			move.Piece,
			capturedPiece,
			promotionPiece,
			secondaryMove,
			previousState.CurrentFen,
			currentState.CurrentFen,
			move.Analysis.IsCheck,
			move.Analysis.IsMate || currentState.IsCheckmate,
			move.Analysis.IsStalemate || currentState.IsStalemate,
			null,
			DateTimeOffset.UtcNow
		);
	}

	public static bool TryBuildFallbackMove(Fen fen, string moveNotation, out Move move)
	{
		var boardState = BoardState.FromFen(fen);
		if (boardState is null)
		{
			move = default;
			return false;
		}

		try
		{
			var analysis = MoveAnalysis.Analyze(moveNotation, boardState.Value, MoveScore.FromCp(0), false);
			move = new(moveNotation, analysis);
			return true;
		}
		catch
		{
			move = default;
			return false;
		}
	}

	public static bool TryCreatePromotionRequest(
		Guid                 gameId,
		long                 pendingPromotionId,
		UciState             currentState,
		string               moveNotation,
		GameMoveActor        actor,
		out PromotionRequiredEvent request)
	{
		request = default;
		if (moveNotation.Length != 4)
			return false;

		var candidates = currentState.LegalMoves
									 .Where(candidate =>
											candidate.Length == 5 &&
											candidate.StartsWith(moveNotation, StringComparison.Ordinal))
									 .ToImmutableArray();
		if (candidates.IsDefaultOrEmpty)
			return false;

		var boardState = BoardState.FromFen(currentState.CurrentFen);
		if (boardState is null || !boardState.Value.TryGetPieceAt(moveNotation[..2], out var movingPiece) || movingPiece is null)
			return false;

		request = new(
			gameId,
			pendingPromotionId,
			actor,
			moveNotation[..2],
			moveNotation[2..4],
			movingPiece.Value,
			candidates.Select(static candidate => candidate[^1] switch
			{
				'q' => PieceType.Queen,
				'r' => PieceType.Rook,
				'b' => PieceType.Bishop,
				'n' => PieceType.Knight,
				_ => PieceType.Queen
			}).Distinct().ToImmutableArray(),
			currentState.CurrentFen,
			DateTimeOffset.UtcNow
		);

		return true;
	}

	public static string? ResolvePromotionMoveNotation(PendingPromotionRequest pending, PieceType pieceType)
	{
		char suffix = pieceType switch
		{
			PieceType.Queen => 'q',
			PieceType.Rook => 'r',
			PieceType.Bishop => 'b',
			PieceType.Knight => 'n',
			_ => '\0'
		};

		if (suffix == '\0')
			return null;

		foreach (var allowed in pending.AllowedPromotionPieces)
		{
			if (allowed != pieceType)
				continue;

			return $"{pending.From}{pending.To}{suffix}";
		}

		return null;
	}

	private static GameMoveKindFlags BuildMoveKindFlags(ParsedMove parsedMove, MoveAnalysis analysis, Piece movingPiece)
	{
		var flags = GameMoveKindFlags.None;
		if (analysis.IsNormal)
			flags |= GameMoveKindFlags.Normal;
		if (analysis.IsCapture)
			flags |= GameMoveKindFlags.Capture;
		if (analysis.IsEnPassant)
			flags |= GameMoveKindFlags.EnPassant;
		if (analysis.IsPromotion)
			flags |= GameMoveKindFlags.Promotion;
		if (analysis.IsCastling)
			flags |= parsedMove.To[0] == 'g'
						 ? GameMoveKindFlags.KingsideCastling
						 : GameMoveKindFlags.QueensideCastling;
		if (movingPiece.Type == PieceType.Pawn &&
			parsedMove.From[0] == parsedMove.To[0] &&
			Math.Abs(parsedMove.To[1] - parsedMove.From[1]) == 2)
			flags |= GameMoveKindFlags.DoublePawnPush;

		return flags == GameMoveKindFlags.None ? GameMoveKindFlags.Normal : flags;
	}

	private static Piece? ResolveCapturedPiece(BoardState? boardState, ParsedMove parsedMove, Move move)
	{
		if (!move.Analysis.IsCapture || boardState is null)
			return null;

		string captureSquare = move.Analysis.IsEnPassant
								   ? $"{parsedMove.To[0]}{parsedMove.From[1]}"
								   : parsedMove.To;

		return boardState.Value.TryGetPieceAt(captureSquare, out var piece) ? piece : null;
	}

	private static PieceMove? ResolveSecondaryPieceMove(BoardState? boardState, ParsedMove parsedMove, Move move)
	{
		if (!move.Analysis.IsCastling || boardState is null)
			return null;

		bool isKingside = parsedMove.To[0] == 'g';
		string rookFrom = isKingside ? $"h{parsedMove.From[1]}" : $"a{parsedMove.From[1]}";
		string rookTo = isKingside ? $"f{parsedMove.From[1]}" : $"d{parsedMove.From[1]}";
		if (!boardState.Value.TryGetPieceAt(rookFrom, out var rookPiece) || rookPiece is null)
			return null;

		return new(rookPiece.Value, rookFrom, rookTo);
	}
}
