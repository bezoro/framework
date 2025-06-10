using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Helpers;
using Bezoro.Chess.Pieces.Models;

namespace Bezoro.Chess.UnitTests.Common.Helpers;

[TestFixture]
public class ChessUtilsIntegrationTests
{
#region Test Methods

	[TestCase(ChessPieceType.King,   typeof(KingModel))]
	[TestCase(ChessPieceType.Queen,  typeof(QueenModel))]
	[TestCase(ChessPieceType.Rook,   typeof(RookModel))]
	[TestCase(ChessPieceType.Bishop, typeof(BishopModel))]
	[TestCase(ChessPieceType.Knight, typeof(KnightModel))]
	[TestCase(ChessPieceType.Pawn,   typeof(PawnModel))]
	public void PieceType_ConsistentWithGetPieceFromChar(ChessPieceType pieceType, Type expectedType)
	{
		// Find the corresponding char (using lowercase for black pieces)
		var correspondingChar = pieceType switch
		{
			ChessPieceType.King   => 'k',
			ChessPieceType.Queen  => 'q',
			ChessPieceType.Rook   => 'r',
			ChessPieceType.Bishop => 'b',
			ChessPieceType.Knight => 'n',
			ChessPieceType.Pawn   => 'p',
			_                     => throw new ArgumentException("Invalid piece type")
		};

		// Verify GetPieceTypeFromChar returns expected piece type
		Assert.That(ChessUtils.GetPieceTypeFromChar(correspondingChar), Is.EqualTo(pieceType));

		// Create piece from char
		var piece = ChessUtils.GetPieceFromChar(correspondingChar);

		// Verify the piece is the right type
		Assert.That(piece, Is.TypeOf(expectedType));
	}

	[TestCase('K', typeof(KingModel),   PlayerColor.White)]
	[TestCase('k', typeof(KingModel),   PlayerColor.Black)]
	[TestCase('Q', typeof(QueenModel),  PlayerColor.White)]
	[TestCase('q', typeof(QueenModel),  PlayerColor.Black)]
	[TestCase('R', typeof(RookModel),   PlayerColor.White)]
	[TestCase('r', typeof(RookModel),   PlayerColor.Black)]
	[TestCase('B', typeof(BishopModel), PlayerColor.White)]
	[TestCase('b', typeof(BishopModel), PlayerColor.Black)]
	[TestCase('N', typeof(KnightModel), PlayerColor.White)]
	[TestCase('n', typeof(KnightModel), PlayerColor.Black)]
	[TestCase('P', typeof(PawnModel),   PlayerColor.White)]
	[TestCase('p', typeof(PawnModel),   PlayerColor.Black)]
	public void RoundtripConversion_PieceCharToPieceAndBack_PreservesOriginalChar(
		char expectedChar,
		Type expectedType,
		PlayerColor expectedColor)
	{
		// 1. Convert char to piece
		var piece = ChessUtils.GetPieceFromChar(expectedChar);

		// 2. Verify the piece is correct
		Assert.Multiple(
			() =>
			{
				Assert.That(piece,       Is.TypeOf(expectedType));
				Assert.That(piece.Color, Is.EqualTo(expectedColor));
			});

		// 3. Convert piece back to char
		var resultChar = ChessUtils.GetCharFromPiece(piece);

		// 4. Verify we get our original char back
		Assert.That(resultChar, Is.EqualTo(expectedChar));
	}

#endregion
}
