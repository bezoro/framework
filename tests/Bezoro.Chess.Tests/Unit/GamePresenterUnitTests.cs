using Bezoro.Chess.Application.Abstractions;
using Bezoro.Chess.Application.Abstractions.ViewModels;
using Bezoro.Chess.Application.Features.PlayGame;
using Bezoro.Chess.Domain.Board;
using Bezoro.Chess.Domain.Moves;
using FluentAssertions;
using NSubstitute;

namespace Bezoro.Chess.Tests.Unit;

public class GamePresenterUnitTests
{
	public GamePresenterUnitTests()
	{
		_mockView    = Substitute.For<IGameView>();
		_presenter   = new(_mockView);
		_gameManager = _presenter.GameManager;
	}

	private readonly GameManager   _gameManager;
	private readonly GamePresenter _presenter;
	private readonly IGameView     _mockView;

	[Fact]
	public void StartNewGame_WhenGameIsFinished_StartsNewGameDirectly()
	{
		// Arrange
		_gameManager.Forfeit(); // Finish the game
		Assert.NotEqual(GameOutcome.Ongoing, _gameManager.Outcome);

		// Act
		_presenter.StartNewGame();

		// Assert
		_mockView.DidNotReceive().ShowConfirmationDialog(Arg.Any<string>(), Arg.Any<Action>(), Arg.Any<Action>());
		// Called once for forfeit, once for new game
		_mockView.Received(2).UpdateBoard(Arg.Any<PieceViewModel[,]>());
		Assert.Equal(GameOutcome.Ongoing, _gameManager.Outcome);
		Assert.Empty(_gameManager.MoveHistory);
	}

	[Fact]
	public void StartNewGame_WithGameInProgress_ShowsConfirmationDialog()
	{
		// Arrange
		// Make a move to have a game in progress
		var move = Move.CreateNormal(new("e2"), new("e4"), new(PieceType.Pawn, PieceColor.White));
		_gameManager.TryMakeMove(move);

		// Act
		_presenter.StartNewGame();

		// Assert
		_mockView.Received(1).ShowConfirmationDialog(Arg.Any<string>(), Arg.Any<Action>(), Arg.Any<Action>());
		// The game should not be reset yet
		Assert.Single(_gameManager.MoveHistory);
	}

	[Fact]
	public void StartNewGame_WithGameInProgressAndCancelled_DoesNotResetGame()
	{
		// Arrange
		var move = Move.CreateNormal(new("e2"), new("e4"), new(PieceType.Pawn, PieceColor.White));
		_gameManager.TryMakeMove(move);

		// Setup mock to immediately invoke the 'onCancel' action (the 3rd argument)
		_mockView.When(v => v.ShowConfirmationDialog(Arg.Any<string>(), Arg.Any<Action>(), Arg.Any<Action>()))
				 .Do(ci => ((Action)ci[2]).Invoke());

		// Act
		_presenter.StartNewGame();

		// Assert
		_mockView.Received(1).ShowConfirmationDialog(Arg.Any<string>(), Arg.Any<Action>(), Arg.Any<Action>());
		Assert.Single(_gameManager.MoveHistory); // Game was not reset
	}

	[Fact]
	public void StartNewGame_WithGameInProgressAndConfirmed_ResetsGame()
	{
		// Arrange
		var move = Move.CreateNormal(new("e2"), new("e4"), new(PieceType.Pawn, PieceColor.White));
		_gameManager.TryMakeMove(move);

		// Setup mock to immediately invoke the 'onConfirm' action (the 2nd argument)
		_mockView.When(v => v.ShowConfirmationDialog(Arg.Any<string>(), Arg.Any<Action>(), Arg.Any<Action>()))
				 .Do(ci => ((Action)ci[1]).Invoke());

		// Act
		_presenter.StartNewGame();

		// Assert
		_mockView.Received(1).ShowConfirmationDialog(Arg.Any<string>(), Arg.Any<Action>(), Arg.Any<Action>());
		_gameManager.MoveHistory.Should().BeEmpty();
		_gameManager.CurrentState.FullMoveNumber.Should().Be(1);
		_gameManager.CurrentState.HalfMoveClock.Should().Be(0);
		_gameManager.CurrentState.ActiveColor.Should().Be(PieceColor.White);
	}

	[Fact]
	public void StartNewGame_WithNoGameInProgress_StartsNewGameDirectly()
	{
		// Arrange
		// Initial state, no moves made

		// Act
		_presenter.StartNewGame();

		// Assert
		_mockView.DidNotReceive().ShowConfirmationDialog(Arg.Any<string>(), Arg.Any<Action>(), Arg.Any<Action>());
		_mockView.Received(1).UpdateBoard(Arg.Any<PieceViewModel[,]>());
		Assert.Equal(GameOutcome.Ongoing, _gameManager.Outcome);
		Assert.Empty(_gameManager.MoveHistory);
	}
}
