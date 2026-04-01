using Bezoro.Chess.UCI.API;
using Bezoro.Chess.UCI.API.Types;
using Bezoro.Chess.UCI.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Tests.API;

[TestSubject(typeof(UciGameEngineSession))]
[Trait("Category", "Integration")]
[Collection("Stockfish")]
public class UciGameEngineSessionGameEngineCompatibilityTests
{
	[Fact]
	public async Task UpdatePositionAsync_WhenPositionIsUpdated_ShouldRaisePositionChangedOnProvidedSynchronizationContext()
	{
		var syncContext = new RecordingSynchronizationContext();
		await using var coordinator = await UciGameEngineSession.CreateAsync(
			TestResourcePaths.STOCKFISH_PATH,
			syncContext: syncContext,
			ct: CancellationToken.None
		);

		SynchronizationContext? handlerContext = null;
		var positionChanged = new TaskCompletionSource<UciState>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.PositionChanged += state =>
		{
			handlerContext = SynchronizationContext.Current;
			if (state.LegalMoves.Count > 0)
				positionChanged.TrySetResult(state);
		};

		await coordinator.UpdatePositionAsync(Fen.Default, null, CancellationToken.None);
		var snapshot = await positionChanged.Task.WaitAsync(TestConstants.DefaultTimeout);

		handlerContext.Should().BeSameAs(syncContext);
		snapshot.CurrentFen.Raw.Should().Be(Fen.Default.Raw);
		snapshot.LegalMoves.Should().NotBeEmpty();
	}

	[Fact]
	public async Task UpdatePositionAsync_WhenMovesAreClassified_ShouldRaiseClassificationEventsOnProvidedSynchronizationContext()
	{
		var syncContext = new RecordingSynchronizationContext();
		await using var coordinator = await UciGameEngineSession.CreateAsync(
			TestResourcePaths.STOCKFISH_PATH,
			syncContext: syncContext,
			ct: CancellationToken.None
		);

		SynchronizationContext? moveContext = null;
		SynchronizationContext? completionContext = null;
		var moveClassified = new TaskCompletionSource<Move>(TaskCreationOptions.RunContinuationsAsynchronously);
		var classificationCompleted =
			new TaskCompletionSource<UciState>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.MoveClassified += move =>
		{
			moveContext = SynchronizationContext.Current;
			moveClassified.TrySetResult(move);
		};

		coordinator.ClassificationCompleted += state =>
		{
			completionContext = SynchronizationContext.Current;
			classificationCompleted.TrySetResult(state);
		};

		await coordinator.UpdatePositionAsync(Fen.Default, null, CancellationToken.None);
		var firstMove = await moveClassified.Task.WaitAsync(TestConstants.DefaultTimeout);
		var completedState = await classificationCompleted.Task.WaitAsync(TestConstants.ExtendedTimeout);

		moveContext.Should().BeSameAs(syncContext);
		completionContext.Should().BeSameAs(syncContext);
		firstMove.Notation.Should().NotBeNullOrWhiteSpace();
		completedState.IsClassificationComplete.Should().BeTrue();
		completedState.ClassifiedMoves.Should().NotBeEmpty();
	}

	[Fact]
	public async Task UpdatePositionAsync_WhenSearchProducesUpdates_ShouldRaiseSearchEventsOnProvidedSynchronizationContext()
	{
		var syncContext = new RecordingSynchronizationContext();
		await using var coordinator = await UciGameEngineSession.CreateAsync(
			TestResourcePaths.STOCKFISH_PATH,
			syncContext: syncContext,
			ct: CancellationToken.None
		);

		SynchronizationContext? searchContext = null;
		SynchronizationContext? evaluationContext = null;
		SynchronizationContext? bestMoveContext = null;
		var searchStateChanged =
			new TaskCompletionSource<UciState>(TaskCreationOptions.RunContinuationsAsynchronously);
		var evaluationChanged =
			new TaskCompletionSource<PrincipalVariation>(TaskCreationOptions.RunContinuationsAsynchronously);
		var bestMoveChanged = new TaskCompletionSource<UciState>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.SearchStateChanged += state =>
		{
			searchContext = SynchronizationContext.Current;
			if (state.IsSearching)
				searchStateChanged.TrySetResult(state);
		};

		coordinator.EvaluationChanged += evaluation =>
		{
			evaluationContext = SynchronizationContext.Current;
			if (evaluation.Moves.Length > 0)
				evaluationChanged.TrySetResult(evaluation);
		};

		coordinator.BestMoveChanged += state =>
		{
			bestMoveContext = SynchronizationContext.Current;
			if (state.BestMove.HasValue)
				bestMoveChanged.TrySetResult(state);
		};

		await coordinator.UpdatePositionAsync(Fen.Default, null, CancellationToken.None);
		var searchingState = await searchStateChanged.Task.WaitAsync(TestConstants.DefaultTimeout);
		var evaluation = await evaluationChanged.Task.WaitAsync(TestConstants.DefaultTimeout);
		var bestMoveState = await bestMoveChanged.Task.WaitAsync(TestConstants.DefaultTimeout);

		searchContext.Should().BeSameAs(syncContext);
		evaluationContext.Should().BeSameAs(syncContext);
		bestMoveContext.Should().BeSameAs(syncContext);
		searchingState.IsSearching.Should().BeTrue();
		evaluation.Moves.Should().NotBeEmpty();
		bestMoveState.BestMove.HasValue.Should().BeTrue();
	}

	[Fact]
	public async Task UpdatePositionAsync_WhenTerminalPositionIsLoaded_ShouldRaiseGameOverOnProvidedSynchronizationContext()
	{
		var syncContext = new RecordingSynchronizationContext();
		await using var coordinator = await UciGameEngineSession.CreateAsync(
			TestResourcePaths.STOCKFISH_PATH,
			syncContext: syncContext,
			ct: CancellationToken.None
		);

		SynchronizationContext? handlerContext = null;
		var gameOver = new TaskCompletionSource<UciState>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.GameOver += state =>
		{
			handlerContext = SynchronizationContext.Current;
			gameOver.TrySetResult(state);
		};

		var checkmateFen = Fen.Parse("7k/6Q1/7K/8/8/8/8/8 b - - 0 1");
		checkmateFen.Should().NotBeNull();

		await coordinator.UpdatePositionAsync(checkmateFen!.Value, null, CancellationToken.None);
		var finalState = await gameOver.Task.WaitAsync(TestConstants.DefaultTimeout);

		handlerContext.Should().BeSameAs(syncContext);
		finalState.IsGameOver.Should().BeTrue();
		finalState.LegalMoves.Should().BeEmpty();
	}
}
