using Bezoro.Chess.ChessLogic;
using Bezoro.Chess.ChessLogic.Presenter;
using Bezoro.Chess.ChessLogic.Presenter.ViewModels;
using NSubstitute;

namespace Bezoro.Chess.Tests.Integration;

public class GamePresenterTests
{
	public GamePresenterTests()
	{
		_view      = Substitute.For<IGameView>();
		_presenter = new(_view);
	}

	private readonly GamePresenter _presenter;
	private readonly IGameView     _view;

	[Fact]
	public void OnSquareSelected_WhenNoPieceIsSelectedAndSquareHasPlayersPiece_HighlightsLegalMoves()
	{
		// Arrange
		_presenter.StartNewGame();
		_view.ClearReceivedCalls();
		var whitePawnStart = new Position("e2");

		// Act
		_presenter.OnSquareSelected(whitePawnStart);

		// Assert
		_view.Received(1).HighlightLegalMoves(
			Arg.Is<IEnumerable<Position>>(
				moves =>
					moves.Count() == 2        &&
					moves.Contains(new("e3")) &&
					moves.Contains(new("e4"))
			));
	}

	[Fact]
	public void OnSquareSelected_WhenPieceIsSelectedAndLegalMoveIsClicked_UpdatesBoard()
	{
		// Arrange
		_presenter.StartNewGame();
		var pawnStart = new Position("e2");
		var pawnEnd   = new Position("e4");

		// Act
		_presenter.OnSquareSelected(pawnStart); // Select the piece
		_view.ClearReceivedCalls();
		_presenter.OnSquareSelected(pawnEnd); // Select the destination

		// Assert
		_view.Received(1).UpdateBoard(
			Arg.Do<PieceViewModel[,]>(
				board =>
				{
					// Assert the pawn is now at e4 and e2 is empty
					Assert.Equal(new(PieceType.Pawn, PieceColor.White), board[4, 4]); // e4
					Assert.Equal(default,                               board[6, 4]); // e2
				}));

		_view.Received(1).HighlightLegalMoves(Arg.Is<IEnumerable<Position>>(moves => !moves.Any()));
	}

	[Fact]
	public void OnSquareSelected_WhenPieceIsSelectedAndSameSquareIsClicked_DeselectsPiece()
	{
		// Arrange
		_presenter.StartNewGame();
		var knightStart = new Position("g1");

		// Act
		_presenter.OnSquareSelected(knightStart); // Select
		_presenter.OnSquareSelected(knightStart); // Select again to deselect

		// Assert
		_view.Received(1).HighlightLegalMoves(Arg.Is<IEnumerable<Position>>(moves => !moves.Any()));
	}

	[Fact]
	public void StartNewGame_WhenCalled_UpdatesViewWithStandardStartPosition()
	{
		// Act
		_presenter.StartNewGame();

		// Assert
		_view.Received(1).UpdateBoard(
			Arg.Do<PieceViewModel[,]>(
				board =>
				{
					// Check a few key pieces to confirm the board is correct
					// Back ranks
					Assert.Equal(new(PieceType.Rook, PieceColor.White),   board[7, 0]);
					Assert.Equal(new(PieceType.Knight, PieceColor.White), board[7, 1]);
					Assert.Equal(new(PieceType.Queen, PieceColor.White),  board[7, 3]);
					Assert.Equal(new(PieceType.Rook, PieceColor.Black),   board[0, 0]);
					Assert.Equal(new(PieceType.Knight, PieceColor.Black), board[0, 1]);

					// Pawns
					for (var col = 0 ; col < 8 ; col++)
					{
						Assert.Equal(new(PieceType.Pawn, PieceColor.White), board[6, col]);
						Assert.Equal(new(PieceType.Pawn, PieceColor.Black), board[1, col]);
					}

					// Empty squares in the middle
					for (var row = 2 ; row < 6 ; row++)
					{
						for (var col = 0 ; col < 8 ; col++)
						{
							Assert.Equal(default, board[row, col]);
						}
					}
				}));
	}
}
