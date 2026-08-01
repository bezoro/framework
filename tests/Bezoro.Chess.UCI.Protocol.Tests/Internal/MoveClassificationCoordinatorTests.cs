using System.Collections.Immutable;
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using Bezoro.Chess.UCI.Protocol.Internal;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.Internal;

[TestSubject(typeof(MoveClassificationCoordinator))]
public class MoveClassificationCoordinatorTests
{
	[Fact]
	public async Task Enqueue_WhenWorkArrivesWhileWorkerRetires_ShouldStartReplacementWorker()
	{
		using var workerRetiring = new ManualResetEventSlim();
		using var releaseRetiringWorker = new ManualResetEventSlim();
		var retirementCalls = 0;
		using var coordinator = new MoveClassificationCoordinator(
			(fen, move) => fen.ClassifyMove(move).WithTacticalOutcome(false, false, false),
			() =>
			{
				if (Interlocked.Increment(ref retirementCalls) != 1)
					return;

				workerRetiring.Set();
				releaseRetiringWorker.Wait();
			}
		);

		coordinator.Enqueue("first", Fen.Default, ImmutableArray.Create("e2e4"));
		await coordinator.WaitAsync("first").WaitAsync(TimeSpan.FromSeconds(5));

		try
		{
			workerRetiring.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
			coordinator.Enqueue("second", Fen.Default, ImmutableArray.Create("d2d4"));

			var completed = await coordinator.WaitAsync("second").WaitAsync(TimeSpan.FromSeconds(5));
			completed["d2d4"].IsResolved.Should().BeTrue();
		}
		finally
		{
			releaseRetiringWorker.Set();
		}
	}

	[Fact]
	public async Task Cancel_WhenRetiredWorkerCompletesAfterReplacement_ShouldIgnoreRetiredResult()
	{
		using var firstClassifierEntered = new ManualResetEventSlim();
		using var firstClassifierReturned = new ManualResetEventSlim();
		using var releaseFirstClassifier = new ManualResetEventSlim();
		using var secondClassifierEntered = new ManualResetEventSlim();
		using var releaseSecondClassifier = new ManualResetEventSlim();
		var classifierCalls = 0;
		using var coordinator = new MoveClassificationCoordinator(
			(fen, move) =>
			{
				if (Interlocked.Increment(ref classifierCalls) == 1)
				{
					firstClassifierEntered.Set();
					releaseFirstClassifier.Wait();
					firstClassifierReturned.Set();
					return fen.ClassifyMove(move).WithTacticalOutcome(true, false, false);
				}

				secondClassifierEntered.Set();
				releaseSecondClassifier.Wait();
				return fen.ClassifyMove(move).WithTacticalOutcome(false, false, true);
			}
		);

		try
		{
			coordinator.Enqueue("starting-position", Fen.Default, ImmutableArray.Create("e2e4"));
			firstClassifierEntered.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
			var retiredWorker = coordinator.ActiveWorker;
			retiredWorker.Should().NotBeNull();
			var retiredWaiter = coordinator.WaitAsync("starting-position");

			coordinator.Cancel();
			coordinator.Enqueue("starting-position", Fen.Default, ImmutableArray.Create("e2e4"));
			secondClassifierEntered.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
			var replacementWorker = coordinator.ActiveWorker;
			replacementWorker.Should().NotBeNull();

			coordinator.Enqueue("starting-position", Fen.Default, ImmutableArray.Create("e2e4"));
			coordinator.ActiveWorker.Should().BeSameAs(replacementWorker);

			releaseFirstClassifier.Set();
			firstClassifierReturned.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
			await retiredWorker!.WaitAsync(TimeSpan.FromSeconds(5));

			coordinator.GetKnown("starting-position")["e2e4"].IsResolved.Should().BeFalse();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => retiredWaiter);

			releaseSecondClassifier.Set();
			var completed = await coordinator.WaitAsync("starting-position").WaitAsync(TimeSpan.FromSeconds(5));
			completed["e2e4"].IsStalemate.Should().BeTrue();
			completed["e2e4"].IsCheck.Should().BeFalse();
			classifierCalls.Should().Be(2);
		}
		finally
		{
			releaseFirstClassifier.Set();
			releaseSecondClassifier.Set();
		}
	}

	[Fact]
	public async Task CancelPendingAndRetain_WhenPositionIsRetained_ShouldKeepCachedClassification()
	{
		using var coordinator = new MoveClassificationCoordinator(
			(fen, move) => fen.ClassifyMove(move).WithTacticalOutcome(true, false, false)
		);
		coordinator.Enqueue("starting-position", Fen.Default, ImmutableArray.Create("e2e4"));
		await coordinator.WaitAsync("starting-position").WaitAsync(TimeSpan.FromSeconds(5));

		coordinator.CancelPendingAndRetain(new HashSet<string>(StringComparer.Ordinal) { "starting-position" });

		var retained = coordinator.GetKnown("starting-position");
		retained["e2e4"].IsResolved.Should().BeTrue();
		retained["e2e4"].IsCheck.Should().BeTrue();
	}

	[Fact]
	public async Task Cancel_WhenPositionIsCached_ShouldClearCachedClassification()
	{
		using var coordinator = new MoveClassificationCoordinator(
			(fen, move) => fen.ClassifyMove(move).WithTacticalOutcome(true, false, false)
		);
		coordinator.Enqueue("starting-position", Fen.Default, ImmutableArray.Create("e2e4"));
		await coordinator.WaitAsync("starting-position").WaitAsync(TimeSpan.FromSeconds(5));

		coordinator.Cancel();

		coordinator.GetKnown("starting-position").Should().BeEmpty();
	}

	[Fact]
	public async Task Dispose_WhenCoordinatorIsReused_ShouldStartFreshGeneration()
	{
		var classifierCalls = 0;
		var coordinator = new MoveClassificationCoordinator(
			(fen, move) =>
			{
				Interlocked.Increment(ref classifierCalls);
				return fen.ClassifyMove(move).WithTacticalOutcome(false, false, false);
			}
		);

		try
		{
			coordinator.Enqueue("first", Fen.Default, ImmutableArray.Create("e2e4"));
			await coordinator.WaitAsync("first").WaitAsync(TimeSpan.FromSeconds(5));
			coordinator.Dispose();

			coordinator.Enqueue("second", Fen.Default, ImmutableArray.Create("d2d4"));
			var completed = await coordinator.WaitAsync("second").WaitAsync(TimeSpan.FromSeconds(5));

			completed["d2d4"].IsResolved.Should().BeTrue();
			classifierCalls.Should().Be(2);
		}
		finally
		{
			coordinator.Dispose();
		}
	}

	[Fact]
	public async Task Enqueue_WhenClassificationIsInProgress_ShouldPublishResolvedMovesProgressively()
	{
		var classifierCalls = 0;
		var knownBeforeSecondClassification = new TaskCompletionSource<ImmutableDictionary<string, MoveClassification>>(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		MoveClassificationCoordinator? coordinator = null;
		coordinator = new(
			(fen, move) =>
			{
				if (Interlocked.Increment(ref classifierCalls) == 2)
					knownBeforeSecondClassification.SetResult(coordinator!.GetKnown("starting-position"));

				return fen.ClassifyMove(move).WithTacticalOutcome(false, false, false);
			}
		);
		using (coordinator)
		{
			coordinator.Enqueue(
				"starting-position",
				Fen.Default,
				ImmutableArray.Create("e2e4", "g1f3")
			);

			var known = await knownBeforeSecondClassification.Task.WaitAsync(TimeSpan.FromSeconds(5));
			known["e2e4"].IsResolved.Should().BeTrue();
			known["g1f3"].IsResolved.Should().BeFalse();

			var completed = await coordinator.WaitAsync("starting-position").WaitAsync(TimeSpan.FromSeconds(5));
			completed.Values.Should().OnlyContain(static classification => classification.IsResolved);
		}
	}

	[Fact]
	public async Task Enqueue_WhenClassifierBlocks_ShouldReturnBeforeClassificationCompletes()
	{
		using var classifierEntered = new ManualResetEventSlim();
		using var releaseClassifier = new ManualResetEventSlim();
		using var coordinator = new MoveClassificationCoordinator(
			(fen, move) =>
			{
				classifierEntered.Set();
				releaseClassifier.Wait();
				return fen.ClassifyMove(move).WithTacticalOutcome(false, false, false);
			}
		);

		var enqueueTask = Task.Run(
			() => coordinator.Enqueue("starting-position", Fen.Default, ImmutableArray.Create("e2e4"))
		);

		try
		{
			classifierEntered.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();

			var completed = await Task.WhenAny(enqueueTask, Task.Delay(TimeSpan.FromSeconds(1)));

			completed.Should().BeSameAs(enqueueTask);
		}
		finally
		{
			releaseClassifier.Set();
			await enqueueTask.WaitAsync(TimeSpan.FromSeconds(5));
		}
	}
}
