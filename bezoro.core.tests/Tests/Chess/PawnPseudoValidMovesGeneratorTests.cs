using System;
using System.Linq;
using Bezoro.Core.Chess;
using Bezoro.Core.Chess.Pieces;
using Bezoro.Core.Chess.Utils;
using NUnit.Framework;

namespace Bezoro.Core.Tests.Chess
{
	[TestFixture]
	public sealed class PawnPseudoValidMovesGeneratorTests
	{
	#region Test Methods

		[Test]
		public void Constructor_CreatesValidInstance()
		{
			// Act
			var generator = new PawnPseudoValidMovesGenerator();

			// Assert
			Assert.Multiple(
				() =>
				{
					Assert.That(generator, Is.Not.Null, "Instance should not be null.");
					Assert.That(
						generator, Is.InstanceOf<PawnPseudoValidMovesGenerator>(),
						"Instance should be of correct concrete type.");

					Assert.That(
						generator, Is.AssignableTo<IPseudoMoveGenerator>(),
						"Instance should implement IPseudoMoveGenerator.");
				});
		}

		[Test]
		public void Generate_AfterPawnHasMoved_ExcludesDoubleAdvance()
		{
			var board = new BoardModel();
			var pawn  = board.GetPieceAt("e2");
			pawn.MarkMoved(); // simulate that the pawn already moved

			var moves = pawn.GetPseudoLegalMoves(board).ToList();

			Assert.That(
				moves.Any(m => m.To.Algebraic == "e4"), Is.False,
				"Double advance should not be generated after the pawn has moved.");
		}

		[Test]
		public void Generate_NullBoard_Throws()
		{
			var g = new PawnPseudoValidMovesGenerator();
			Assert.Throws<ArgumentNullException>(() => g.Generate(null!, new PawnModel(PlayerColor.White)).ToList());
		}

		[Test]
		public void Generate_NullPiece_Throws()
		{
			var g     = new PawnPseudoValidMovesGenerator();
			var board = new BoardModel();
			Assert.Throws<ArgumentNullException>(() => g.Generate(board, null!).ToList());
		}

		[Test]
		public void Generate_PawnOn7thRank_EmitsPromotionMoves()
		{
			var board = new BoardModel();
			var pawn  = board.GetPieceAt("a2");

			board.MovePiece(pawn, "a2", "a7");
			pawn.ResetMoved(); // ignore first-move flag

			var moves = pawn.GetPseudoLegalMoves(board).ToList();
			var promos = moves
						 .Where(m => m.Kind == MoveKind.Promotion)
						 .ToList();

			Assert.That(
				promos, Has.Count.EqualTo(4),
				"Single-push promotion should yield 4 moves.");

			// `PromoteTo` is guaranteed non-null for promotion moves → unwrap with !
			var promotedPieceKinds = promos
									 .Select(m => m.PromoteTo!.Value)
									 .Distinct();

			Assert.That(
				promotedPieceKinds,
				Is.EquivalentTo(
					new[]
					{
						PromotionPieceType.Queen,
						PromotionPieceType.Rook,
						PromotionPieceType.Bishop,
						PromotionPieceType.Knight
					}));
		}

		[Test]
		public void Generate_StartingPawn_ReturnsAllPseudoValidMoves()
		{
			// Arrange
			var board = new BoardModel();
			var pawn  = board.GetPieceAt("c2");

			// Act
			var moves = pawn.GetPseudoLegalMoves(board).ToList();

			// Assert
			var expectedMoves = new[]
			{
				(Square: "c3", Kind: MoveKind.Normal),
				(Square: "c4", Kind: MoveKind.Normal),
				(Square: "b3", Kind: MoveKind.Capture),
				(Square: "d3", Kind: MoveKind.Capture),
				(Square: "b3", Kind: MoveKind.EnPassant),
				(Square: "d3", Kind: MoveKind.EnPassant)
			};

			Assert.Multiple(
				() =>
				{
					Assert.That(moves, Has.Count.EqualTo(6), "A pawn should generate 6 pseudo-valid moves in total.");

					for (var i = 0 ; i < expectedMoves.Length ; i++)
					{
						var (square, kind) = expectedMoves[i];
						Assert.That(moves[i].To.Algebraic, Is.EqualTo(square), $"Move {i + 1} should be to {square}");
						Assert.That(moves[i].Kind,         Is.EqualTo(kind),   $"Move {i + 1} should be a {kind} move");
					}
				});
		}

		[Test]
		public void Generate_WithNonPawnPiece_Throws()
		{
			var g     = new PawnPseudoValidMovesGenerator();
			var board = new BoardModel();
			var rook  = board.GetPieceAt("a1"); // or new RookModel(...)
			Assert.Throws<ArgumentException>(() => g.Generate(board, rook).ToList());
		}

	#endregion
	}
}
