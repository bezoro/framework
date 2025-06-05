using System;
using Bezoro.Core.Chess.Common.Enums;
using Bezoro.Core.Chess.Common.Helpers;
using Bezoro.Core.Chess.Pieces.Models;
using NUnit.Framework;

namespace Bezoro.Core.Chess.Tests.Common.Helpers
{
	[TestFixture]
	public class ChessUtilsTests
	{
	#region GetCharFromPiece

		[TestCase(PlayerColor.White, typeof(KingModel),   'K')]
		[TestCase(PlayerColor.Black, typeof(KingModel),   'k')]
		[TestCase(PlayerColor.White, typeof(QueenModel),  'Q')]
		[TestCase(PlayerColor.Black, typeof(QueenModel),  'q')]
		[TestCase(PlayerColor.White, typeof(RookModel),   'R')]
		[TestCase(PlayerColor.Black, typeof(RookModel),   'r')]
		[TestCase(PlayerColor.White, typeof(BishopModel), 'B')]
		[TestCase(PlayerColor.Black, typeof(BishopModel), 'b')]
		[TestCase(PlayerColor.White, typeof(KnightModel), 'N')]
		[TestCase(PlayerColor.Black, typeof(KnightModel), 'n')]
		[TestCase(PlayerColor.White, typeof(PawnModel),   'P')]
		[TestCase(PlayerColor.Black, typeof(PawnModel),   'p')]
		public void GetCharFromPiece_ReturnsExpectedChar(
			PlayerColor color,
			Type pieceType,
			char expected)
		{
			var piece  = (PieceModel)Activator.CreateInstance(pieceType, color)!;
			var result = ChessUtils.GetCharFromPiece(piece);

			Assert.That(result, Is.EqualTo(expected));
		}

		[Test]
		public void GetCharFromPiece_UnknownModel_Throws()
		{
			var dummyPiece = new DummyPiece(PlayerColor.White);

			Assert.Throws<ArgumentException>(() => ChessUtils.GetCharFromPiece(dummyPiece));
		}

		private sealed class DummyPiece : PieceModel
		{
			public DummyPiece(PlayerColor color) : base(color, null) { }
		}

	#endregion

	#region GetPieceTypeFromChar

		[TestCase('k', ChessPieceType.King)]
		[TestCase('q', ChessPieceType.Queen)]
		[TestCase('r', ChessPieceType.Rook)]
		[TestCase('b', ChessPieceType.Bishop)]
		[TestCase('n', ChessPieceType.Knight)]
		[TestCase('p', ChessPieceType.Pawn)]
		public void GetPieceTypeFromChar_ValidChar_ReturnsExpectedType(
			char fen,
			ChessPieceType expected)
		{
			var result = ChessUtils.GetPieceTypeFromChar(fen);

			Assert.That(result, Is.EqualTo(expected));
		}

		[Test]
		public void GetPieceTypeFromChar_InvalidChar_Throws() =>
			Assert.Throws<ArgumentException>(() => ChessUtils.GetPieceTypeFromChar('x'));

	#endregion

	#region GetPieceFromChar

		[TestCase('k', typeof(KingModel),   PlayerColor.Black)]
		[TestCase('K', typeof(KingModel),   PlayerColor.White)]
		[TestCase('q', typeof(QueenModel),  PlayerColor.Black)]
		[TestCase('Q', typeof(QueenModel),  PlayerColor.White)]
		[TestCase('r', typeof(RookModel),   PlayerColor.Black)]
		[TestCase('R', typeof(RookModel),   PlayerColor.White)]
		[TestCase('b', typeof(BishopModel), PlayerColor.Black)]
		[TestCase('B', typeof(BishopModel), PlayerColor.White)]
		[TestCase('n', typeof(KnightModel), PlayerColor.Black)]
		[TestCase('N', typeof(KnightModel), PlayerColor.White)]
		[TestCase('p', typeof(PawnModel),   PlayerColor.Black)]
		[TestCase('P', typeof(PawnModel),   PlayerColor.White)]
		public void GetPieceFromChar_ValidChar_ReturnsCorrectModelAndColor(
			char fen,
			Type expectedType,
			PlayerColor expectedColor)
		{
			var piece = ChessUtils.GetPieceFromChar(fen);

			Assert.Multiple(
				() =>
				{
					Assert.That(piece,       Is.TypeOf(expectedType));
					Assert.That(piece.Color, Is.EqualTo(expectedColor));
				});
		}

		[Test]
		public void GetPieceFromChar_InvalidChar_Throws() =>
			Assert.Throws<ArgumentException>(() => ChessUtils.GetPieceFromChar('X'));

	#endregion
	}
}
