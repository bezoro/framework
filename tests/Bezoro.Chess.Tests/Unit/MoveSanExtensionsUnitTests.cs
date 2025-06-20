using Bezoro.Chess.Domain.Extensions;
using Bezoro.Chess.Domain.Helpers;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Tests.Unit;

public class MoveSanExtensionsUnitTests
{
	[Theory]
	[InlineData(
		"rnbqkbnr/ppp1pppp/8/3p4/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 1",
		"e4", "d5", PieceType.Pawn, "exd5")]
	[InlineData(
		"rnbqkb1r/ppp1pppp/5n2/3p4/4P3/2N5/PPPP1PPP/R1BQKBNR b KQkq - 1 3",
		"f6", "e4", PieceType.Knight, "Nxe4")]
	internal void ToSan_ShouldReturnCorrectSan_ForCaptures(
		string fen, string from, string to, PieceType pieceType, string expectedSan)
	{
		// Arrange
		GameState gameState     = FenParser.FenToGameState(fen);
		var       fromPos       = new Position(from);
		var       toPos         = new Position(to);
		var       piece         = new Piece(pieceType, gameState.ActiveColor);
		Piece     capturedPiece = gameState.PiecePositions[toPos.Row, toPos.Col];
		var       move          = Move.CreateCapture(fromPos, toPos, piece, capturedPiece);

		// Act
		string san = move.ToSAN(gameState);

		// Assert
		Assert.Equal(expectedSan, san);
	}

	[Theory]
	[InlineData(
		"r1bqk2r/pppp1ppp/2n2n2/2b1p3/2B1P3/3P1N2/PPP2PPP/RNBQ1RK1 b kq - 0 5",
		MoveType.CastleKingside, "O-O")]
	[InlineData(
		"r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
		MoveType.CastleQueenside, "O-O-O")]
	internal void ToSan_ShouldReturnCorrectSan_ForCastling(string fen, MoveType moveType, string expectedSan)
	{
		// Arrange
		GameState gameState = FenParser.FenToGameState(fen);
		Position  kingPos   = gameState.FindKingPosition(gameState.ActiveColor)!.Value;
		Piece     king      = gameState.PiecePositions[kingPos.Row, kingPos.Col];

		Move move;
		if (moveType == MoveType.CastleKingside)
		{
			var toPos = new Position(kingPos.Row, kingPos.Col + 2);
			move = Move.CreateCastleKingside(kingPos, toPos, king);
		}
		else // Queenside
		{
			var toPos = new Position(kingPos.Row, kingPos.Col - 2);
			move = Move.CreateCastleQueenside(kingPos, toPos, king);
		}

		// Act
		string result = move.ToSAN(gameState);

		// Assert
		Assert.Equal(expectedSan, result);
	}

	[Theory]
	[InlineData(
		"rnbqkbnr/pppp1ppp/8/4p3/6P1/5P2/PPPPP2P/RNBQKBNR b KQkq - 0 2",
		"d8", "h4", PieceType.Queen, "Qh4#")]
	[InlineData(
		"7k/R7/8/8/8/3B4/8/6R1 w - - 0 1",
		"a7", "a8", PieceType.Rook, "Ra8#")]
	internal void ToSan_ShouldReturnCorrectSan_ForCheckmates(
		string fen, string from, string to, PieceType pieceType, string expectedSan)
	{
		// Arrange
		GameState gameState = FenParser.FenToGameState(fen);
		var       fromPos   = new Position(from);
		var       toPos     = new Position(to);
		var       piece     = new Piece(pieceType, gameState.ActiveColor);
		var       move      = Move.CreateNormal(fromPos, toPos, piece);

		// Act
		string san = move.ToSAN(gameState);

		// Assert
		Assert.Equal(expectedSan, san);
	}

	[Theory]
	[InlineData("5k2/8/8/8/8/8/8/4K2R w K - 0 1", "h1", "h8", PieceType.Rook,   "Rh8+")]
	[InlineData("8/5k2/8/8/8/8/8/4KB2 w - - 0 1", "f1", "c4", PieceType.Bishop, "Bc4+")]
	internal void ToSan_ShouldReturnCorrectSan_ForChecks(
		string fen, string from, string to, PieceType pieceType, string expectedSan)
	{
		// Arrange
		GameState gameState = FenParser.FenToGameState(fen);
		var       fromPos   = new Position(from);
		var       toPos     = new Position(to);
		var       piece     = new Piece(pieceType, gameState.ActiveColor);
		var       move      = Move.CreateNormal(fromPos, toPos, piece);

		// Act
		string san = move.ToSAN(gameState);

		// Assert
		Assert.Equal(expectedSan, san);
	}

	[Theory]
	[InlineData(
		"rnbqkb1r/pp5P/2p1p3/3p4/8/8/PPPP1PP1/RNBQKBNR w KQkq - 0 8",
		"h7", "h8", PieceType.Pawn, MoveType.PawnPromotion, PromotionType.Queen, "h8=Q")]
	[InlineData(
		"r1b2k1r/p1Ppppb1/1n2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQ - 1 2",
		"c7", "b8", PieceType.Pawn, MoveType.PawnPromotionCapture, PromotionType.Rook, "cxb8=R")]
	internal void ToSan_ShouldReturnCorrectSan_ForPromotions(
		string fen, string from, string to, PieceType pieceType, MoveType moveType, PromotionType promotionPiece,
		string expectedSan)
	{
		// Arrange
		GameState gameState = FenParser.FenToGameState(fen);
		var       fromPos   = new Position(from);
		var       toPos     = new Position(to);
		var       pawn      = new Piece(pieceType, gameState.ActiveColor);

		Move move;
		if (moveType == MoveType.PawnPromotion)
		{
			move = Move.CreateQuietPromotion(fromPos, toPos, pawn, promotionPiece);
		}
		else // PawnPromotionCapture
		{
			Piece capturedPiece = gameState.PiecePositions[toPos.Row, toPos.Col];
			move = Move.CreateCapturePromotion(fromPos, toPos, pawn, capturedPiece, promotionPiece);
		}

		// Act
		string san = move.ToSAN(gameState);

		// Assert
		Assert.Equal(expectedSan, san);
	}

	[Theory]
	[InlineData(
		"rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1",
		"e2", "e4", PieceType.Pawn, MoveType.Normal, "e4")]
	[InlineData(
		"rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1",
		"e7", "e5", PieceType.Pawn, MoveType.Normal, "e5")]
	[InlineData(
		"rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 1",
		"g1", "f3", PieceType.Knight, MoveType.Normal, "Nf3")]
	[InlineData(
		"rnbqkbnr/pppp1ppp/8/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R b KQkq - 1 2",
		"b8", "c6", PieceType.Knight, MoveType.Normal, "Nc6")]
	[InlineData(
		"r1bqkbnr/pppp1ppp/2n5/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 2 3",
		"f1", "c4", PieceType.Bishop, MoveType.Normal, "Bc4")]
	[InlineData(
		"r1bqkbnr/pppp1ppp/2n5/4p3/2B1P3/5N2/PPPP1PPP/RNBQK2R b KQkq - 3 4",
		"f8", "c5", PieceType.Bishop, MoveType.Normal, "Bc5")]
	[InlineData(
		"r1bqk1nr/pppp1ppp/2n5/2b1p3/2B1P3/5N2/PPPP1PPP/RNBQ1RK1 b kq - 5 5",
		"a8", "b8", PieceType.Rook, MoveType.Normal, "Rb8")]
	[InlineData(
		"rnbqkbnr/pp1ppppp/8/2p5/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2",
		"d1", "h5", PieceType.Queen, MoveType.Normal, "Qh5")]
	internal void ToSan_ShouldReturnCorrectSan_ForSimpleMoves(
		string fen, string from, string to, PieceType pieceType, MoveType moveType, string expectedSan)
	{
		// Arrange
		GameState gameState = FenParser.FenToGameState(fen);
		var       fromPos   = new Position(from);
		var       toPos     = new Position(to);
		var       piece     = new Piece(pieceType, gameState.ActiveColor);
		var       move      = Move.CreateNormal(fromPos, toPos, piece);

		// Act
		string san = move.ToSAN(gameState);

		// Assert
		Assert.Equal(expectedSan, san);
	}
}
