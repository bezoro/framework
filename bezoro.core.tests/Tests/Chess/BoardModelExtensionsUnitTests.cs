using System;
using System.Collections.Generic;
using Bezoro.Core.Chess.Abstractions.Interfaces;
using Bezoro.Core.Chess.Board;
using Bezoro.Core.Chess.Common.Enums;
using Bezoro.Core.Chess.Common.Extensions;
using Bezoro.Core.Chess.Game.Models;
using Bezoro.Core.Chess.Moves.Models;
using NUnit.Framework;

namespace Bezoro.Core.Chess.Tests.Common
{
	[TestFixture]
	public class BoardModelExtensionsTests
	{
		private FakeBoard _board;

	#region Setup/Teardown Methods

		[SetUp]
		public void SetUp() =>
			_board = new(8, 8);

	#endregion

	#region Happy paths ----------------------------------------------------

		[Test]
		public void GetSquareAt_String_ReturnsExpectedSquare()
		{
			var expected = _board.Squares[4, 3]; // e4 -> (4,3)
			var actual   = _board.GetSquareAt("e4");

			Assert.That(actual, Is.SameAs(expected));
		}

		[Test]
		public void GetPieceAt_String_ReturnsExpectedPiece()
		{
			var rook = new FakePiece();
			_board.Squares[4, 3].SetPiece(rook); // place on e4

			var actual = _board.GetPieceAt("e4");

			Assert.That(actual, Is.SameAs(rook));
		}

		[Test]
		public void GetPiecePlacementFen_EmptyBoard_ReturnsAllEmptyString()
		{
			var fen = _board.GetPiecePlacementFen();

			Assert.That(fen, Is.EqualTo("8/8/8/8/8/8/8/8"));
		}

		[Test]
		public void CreatePieceAt_String_PlacesPieceOnTargetSquare()
		{
			Assert.That(_board.GetPieceAt("a1"), Is.Null); // pre-condition

			_board.CreatePieceAt("a1", PlayerColor.White, ChessPieceType.King);

			Assert.That(_board.GetPieceAt("a1"), Is.Not.Null);
		}

	#endregion

	#region Minimal fakes --------------------------------------------------

		private sealed class FakeBoard : IChessBoardModel
		{
			public FakeBoard(int width, int height)
			{
				Width   = width;
				Height  = height;
				Squares = new IChessBoardSquareModel[width, height];

				for (var x = 0 ; x < width ; x++)
				{
					for (var y = 0 ; y < height ; y++)
						Squares[x, y] = new FakeSquare(new(x, y));
				}
			}

			public IChessBoardSquareModel[,] Squares { get; }
			public int                       Height  { get; }

			public int Width { get; }

			/* --- members not required by the extension code --- */
			public List<IChessPieceModel> BoardPieces => new();

		#region Interface Implementations

			public BoardPosition? GetPosition(IChessPieceModel piece) => null;

			public IEnumerable<IChessBoardSquareModel> GetStraightPath(BoardPosition from, BoardPosition to)
				=> throw new NotImplementedException();

			public bool IsEmpty(BoardPosition position) => this.GetSquareAt(position.Column, position.Row).IsEmpty;
			public bool IsSquareAttacked(BoardPosition position, PlayerColor byColor) => false;

			public void MovePiece(IChessPieceModel piece, string from, string to) =>
				throw new NotImplementedException();

			public void MovePiece(IChessPieceModel piece, BoardPosition from, BoardPosition to) =>
				throw new NotImplementedException();

			public void SetPieceAt(IChessPieceModel piece, IChessBoardSquareModel square) =>
				square.SetPiece(piece);

		#endregion

			public bool IsInside(int col, int row) =>
				col >= 0 && col < Width && row >= 0 && row < Height;
		}

		private sealed class FakeSquare : IChessBoardSquareModel
		{
			public FakeSquare(BoardPosition position)
			{
				Position = position;
			}

			public BoardPosition Position { get; }

			public bool IsEmpty    => Piece is null;
			public bool IsOccupied => Piece is not null;

			public IChessPieceModel? Piece { get; private set; }

		#region Interface Implementations

			public void SetPiece(IChessPieceModel? piece) => Piece = piece;
			public void ClearPiece() => Piece = null;
			public IChessPieceModel? GetPiece() => Piece;
			public void RemovePiece(IChessPieceModel piece) => ClearPiece();

		#endregion
		}

		private sealed class FakePiece : IChessPieceModel
		{
			public PlayerColor Color    => PlayerColor.White;
			public PlayerColor Opposite => PlayerColor.Black;
			public bool        HasMoved { get; private set; }

		#region Interface Implementations

			public ChessPieceType GetPieceType() => ChessPieceType.Rook;
			public IEnumerable<Move> GetPseudoLegalMoves(GameModel _) => Array.Empty<Move>();
			public void MarkMoved() => HasMoved = true;
			public void ResetMoved() => HasMoved = false;

		#endregion
		}

	#endregion
	}
}
