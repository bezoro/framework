using Bezoro.Chess.UCI.API;
using Bezoro.Chess.UCI.API.Common.Enums;
using Bezoro.Chess.UCI.API.Types;
using Bezoro.Chess.UCI.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Tests.API;

[TestSubject(typeof(UciCoordinator))]
[Trait("Category", "Integration")]
[Collection("Stockfish")]
public class UciCoordinatorGameEventModelTests
{
	[Fact]
	public async Task MakeMoveAsync_WhenNormalMoveIsApplied_ShouldRaiseOrderedRichEventsOnProvidedSynchronizationContext()
	{
		var syncContext = new RecordingSynchronizationContext();
		await using var coordinator = await UciCoordinator.CreateAsync(
			TestResourcePaths.STOCKFISH_PATH,
			syncContext: syncContext,
			ct: CancellationToken.None
		);

		await coordinator.UpdatePositionAsync(Fen.Default, null, CancellationToken.None);

		var eventOrder = new List<string>();
		SynchronizationContext? moveContext = null;
		var moveMade = new TaskCompletionSource<GameMoveEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
		var turnChanged = new TaskCompletionSource<TurnChangedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
		var positionChanged = new TaskCompletionSource<UciState>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.MoveMade += payload =>
		{
			moveContext = SynchronizationContext.Current;
			eventOrder.Add(nameof(coordinator.MoveMade));
			moveMade.TrySetResult(payload);
		};

		coordinator.TurnChanged += payload =>
		{
			eventOrder.Add(nameof(coordinator.TurnChanged));
			turnChanged.TrySetResult(payload);
		};

		coordinator.PositionChanged += state =>
		{
			if (state.PlayedMoves.Count != 1)
				return;

			eventOrder.Add(nameof(coordinator.PositionChanged));
			positionChanged.TrySetResult(state);
		};

		await coordinator.MakeMoveAsync("e2e4", CancellationToken.None);

		var movePayload = await moveMade.Task.WaitAsync(TestConstants.DefaultTimeout);
		var turnPayload = await turnChanged.Task.WaitAsync(TestConstants.DefaultTimeout);
		var state = await positionChanged.Task.WaitAsync(TestConstants.DefaultTimeout);

		moveContext.Should().BeSameAs(syncContext);
		eventOrder.Should().ContainInOrder(
			nameof(coordinator.MoveMade),
			nameof(coordinator.TurnChanged),
			nameof(coordinator.PositionChanged)
		);

		movePayload.Actor.Should().Be(GameMoveActor.Human);
		movePayload.Notation.Should().Be("e2e4");
		movePayload.From.Should().Be("e2");
		movePayload.To.Should().Be("e4");
		movePayload.MovingPiece.Type.Should().Be(PieceType.Pawn);
		movePayload.KindFlags.HasFlag(GameMoveKindFlags.DoublePawnPush).Should().BeTrue();
		movePayload.PreviousFen.Raw.Should().Be(Fen.Default.Raw);
		movePayload.ResultingFen.Raw.Should().Be(state.CurrentFen.Raw);
		turnPayload.PreviousTurn.Should().Be('w');
		turnPayload.CurrentTurn.Should().Be('b');
	}

	[Fact]
	public async Task MakeMoveAsync_WhenCaptureIsApplied_ShouldRaiseCaptureMadeWithTypedPayload()
	{
		var syncContext = new RecordingSynchronizationContext();
		await using var coordinator = await UciCoordinator.CreateAsync(
			TestResourcePaths.STOCKFISH_PATH,
			syncContext: syncContext,
			ct: CancellationToken.None
		);

		await coordinator.UpdatePositionAsync(Fen.Default, ["e2e4", "d7d5"], CancellationToken.None);

		SynchronizationContext? captureContext = null;
		var captureMade = new TaskCompletionSource<GameMoveEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.CaptureMade += payload =>
		{
			captureContext = SynchronizationContext.Current;
			captureMade.TrySetResult(payload);
		};

		await coordinator.MakeMoveAsync("e4d5", CancellationToken.None);

		var payload = await captureMade.Task.WaitAsync(TestConstants.DefaultTimeout);

		captureContext.Should().BeSameAs(syncContext);
		payload.Notation.Should().Be("e4d5");
		payload.KindFlags.HasFlag(GameMoveKindFlags.Capture).Should().BeTrue();
		payload.MovingPiece.Type.Should().Be(PieceType.Pawn);
		payload.CapturedPiece.Should().NotBeNull();
		payload.CapturedPiece!.Value.Type.Should().Be(PieceType.Pawn);
		payload.CapturedPiece.Value.Color.Should().Be(PieceColor.Black);
	}

	[Fact]
	public async Task MakeMoveAsync_WhenPromotionChoiceIsRequired_ShouldRaisePromotionRequiredWithoutChangingPosition()
	{
		var syncContext = new RecordingSynchronizationContext();
		await using var coordinator = await UciCoordinator.CreateAsync(
			TestResourcePaths.STOCKFISH_PATH,
			syncContext: syncContext,
			ct: CancellationToken.None
		);

		var fen = Fen.Parse("1r5k/P7/8/8/8/8/8/K7 w - - 0 1");
		fen.Should().NotBeNull();
		await coordinator.UpdatePositionAsync(fen!.Value, null, CancellationToken.None);

		SynchronizationContext? requestContext = null;
		var promotionRequired =
			new TaskCompletionSource<PromotionRequiredEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.PromotionRequired += payload =>
		{
			requestContext = SynchronizationContext.Current;
			promotionRequired.TrySetResult(payload);
		};

		var state = await coordinator.MakeMoveAsync("a7a8", CancellationToken.None);
		var request = await promotionRequired.Task.WaitAsync(TestConstants.DefaultTimeout);

		requestContext.Should().BeSameAs(syncContext);
		request.From.Should().Be("a7");
		request.To.Should().Be("a8");
		request.MovingPiece.Type.Should().Be(PieceType.Pawn);
		request.AllowedPromotionPieces.Should().Equal(
			[PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight]
		);
		state.CurrentFen.Raw.Should().Be(fen.Value.Raw);
		state.PlayedMoves.Should().BeEmpty();
	}

	[Fact]
	public async Task ChoosePromotionAsync_WhenPendingPromotionIsResolved_ShouldRaisePromotionChosenThenMoveMade()
	{
		var syncContext = new RecordingSynchronizationContext();
		await using var coordinator = await UciCoordinator.CreateAsync(
			TestResourcePaths.STOCKFISH_PATH,
			syncContext: syncContext,
			ct: CancellationToken.None
		);

		var fen = Fen.Parse("1r5k/P7/8/8/8/8/8/K7 w - - 0 1");
		fen.Should().NotBeNull();
		await coordinator.UpdatePositionAsync(fen!.Value, null, CancellationToken.None);

		var order = new List<string>();
		var promotionRequired =
			new TaskCompletionSource<PromotionRequiredEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
		var promotionChosen =
			new TaskCompletionSource<PromotionChosenEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
		var moveMade = new TaskCompletionSource<GameMoveEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.PromotionRequired += payload => promotionRequired.TrySetResult(payload);
		coordinator.PromotionChosen += payload =>
		{
			order.Add(nameof(coordinator.PromotionChosen));
			promotionChosen.TrySetResult(payload);
		};
		coordinator.MoveMade += payload =>
		{
			order.Add(nameof(coordinator.MoveMade));
			moveMade.TrySetResult(payload);
		};

		await coordinator.MakeMoveAsync("a7a8", CancellationToken.None);
		var request = await promotionRequired.Task.WaitAsync(TestConstants.DefaultTimeout);

		await coordinator.ChoosePromotionAsync(request.PendingPromotionId, PieceType.Queen, CancellationToken.None);

		var chosen = await promotionChosen.Task.WaitAsync(TestConstants.DefaultTimeout);
		var movePayload = await moveMade.Task.WaitAsync(TestConstants.DefaultTimeout);

		order.Should().ContainInOrder(nameof(coordinator.PromotionChosen), nameof(coordinator.MoveMade));
		chosen.PieceType.Should().Be(PieceType.Queen);
		movePayload.Notation.Should().Be("a7a8q");
		movePayload.KindFlags.HasFlag(GameMoveKindFlags.Promotion).Should().BeTrue();
		movePayload.PromotionPiece.Should().NotBeNull();
		movePayload.PromotionPiece!.Value.Type.Should().Be(PieceType.Queen);
	}

	[Fact]
	public async Task MakeMoveAsync_WhenMoveIsIllegal_ShouldRaiseIllegalMoveRejectedOnProvidedSynchronizationContext()
	{
		var syncContext = new RecordingSynchronizationContext();
		await using var coordinator = await UciCoordinator.CreateAsync(
			TestResourcePaths.STOCKFISH_PATH,
			syncContext: syncContext,
			ct: CancellationToken.None
		);

		await coordinator.UpdatePositionAsync(Fen.Default, null, CancellationToken.None);

		SynchronizationContext? handlerContext = null;
		var rejected =
			new TaskCompletionSource<IllegalMoveRejectedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.IllegalMoveRejected += payload =>
		{
			handlerContext = SynchronizationContext.Current;
			rejected.TrySetResult(payload);
		};

		await FluentActions.Awaiting(() => coordinator.MakeMoveAsync("e2e5", CancellationToken.None))
						   .Should()
						   .ThrowAsync<ArgumentException>();

		var payload = await rejected.Task.WaitAsync(TestConstants.DefaultTimeout);

		handlerContext.Should().BeSameAs(syncContext);
		payload.AttemptedMove.Should().Be("e2e5");
		payload.LegalMoves.Should().Contain("e2e4");
	}

	[Fact]
	public async Task UndoAsync_WhenMovesAreUndone_ShouldRaiseMoveUndoneTurnChangedAndPositionChanged()
	{
		var syncContext = new RecordingSynchronizationContext();
		await using var coordinator = await UciCoordinator.CreateAsync(
			TestResourcePaths.STOCKFISH_PATH,
			syncContext: syncContext,
			ct: CancellationToken.None
		);

		await coordinator.UpdatePositionAsync(Fen.Default, null, CancellationToken.None);
		await coordinator.MakeMoveAsync("e2e4", CancellationToken.None);
		await coordinator.MakeMoveAsync("e7e5", GameMoveActor.Engine, CancellationToken.None);

		var order = new List<string>();
		var moveUndone = new TaskCompletionSource<MoveUndoneEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

		coordinator.MoveUndone += payload =>
		{
			order.Add(nameof(coordinator.MoveUndone));
			moveUndone.TrySetResult(payload);
		};
		coordinator.TurnChanged += _ => order.Add(nameof(coordinator.TurnChanged));
		coordinator.PositionChanged += state =>
		{
			if (state.PlayedMoves.Count == 1)
				order.Add(nameof(coordinator.PositionChanged));
		};

		var state = await coordinator.UndoAsync(1, CancellationToken.None);
		var payload = await moveUndone.Task.WaitAsync(TestConstants.DefaultTimeout);

		order.Should().ContainInOrder(
			nameof(coordinator.MoveUndone),
			nameof(coordinator.TurnChanged),
			nameof(coordinator.PositionChanged)
		);
		payload.Moves.Should().ContainSingle();
		payload.Moves[0].Notation.Should().Be("e7e5");
		state.PlayedMoves.Should().Equal(["e2e4"]);
	}
}
