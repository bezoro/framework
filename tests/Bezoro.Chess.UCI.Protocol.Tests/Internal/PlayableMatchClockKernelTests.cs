using Bezoro.Chess.UCI.Protocol.API.Types;
using Bezoro.Chess.UCI.Protocol.Internal;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.Internal;

[TestSubject(typeof(PlayableMatchClockKernel))]
public sealed class PlayableMatchClockKernelTests
{
    private static readonly DateTimeOffset Origin = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CompleteMove_WhenElapsedEqualsRemaining_ShouldNotGrantIncrementOrCountMove()
    {
        var timeControl = new PlayableMatchTimeControl(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(3));
        var checkpoint = CreateCheckpoint(whiteRemaining: 5);

        var completed = PlayableMatchClockKernel.CompleteMove(
            timeControl,
            checkpoint,
            'w',
            Origin + TimeSpan.FromSeconds(5));

        completed.WhiteRemaining.Should().Be(TimeSpan.Zero);
        completed.WhiteMovesCompleted.Should().Be(0);
        completed.ActiveColor.Should().Be('b');
    }

    [Fact]
    public void CompleteMove_WhenElapsedIsWithinDelay_ShouldGrantIncrementWithoutDebit()
    {
        var timeControl = new PlayableMatchTimeControl(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5));
        var checkpoint = CreateCheckpoint();

        var completed = PlayableMatchClockKernel.CompleteMove(
            timeControl,
            checkpoint,
            'w',
            Origin + TimeSpan.FromSeconds(3));

        completed.WhiteRemaining.Should().Be(TimeSpan.FromSeconds(32));
        completed.WhiteMovesCompleted.Should().Be(1);
    }

    [Fact]
    public void CompleteMove_WhenElapsedExceedsDelay_ShouldDebitOnlyExcess()
    {
        var timeControl = new PlayableMatchTimeControl(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5));
        var checkpoint = CreateCheckpoint();

        var completed = PlayableMatchClockKernel.CompleteMove(
            timeControl,
            checkpoint,
            'w',
            Origin + TimeSpan.FromSeconds(8));

        completed.WhiteRemaining.Should().Be(TimeSpan.FromSeconds(28));
        completed.WhiteMovesCompleted.Should().Be(1);
    }

    [Fact]
    public void CompleteMove_WhenStagesAreUnsortedAndDuplicated_ShouldUseLastEligibleStageAndFirstMatchingAddedTime()
    {
        var timeControl = new PlayableMatchTimeControl(
            TimeSpan.FromSeconds(100),
            TimeSpan.Zero,
            TimeSpan.Zero,
            additionalStages:
            [
                new(3, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3)),
                new(1, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)),
                new(3, TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4))
            ]);
        var checkpoint = CreateCheckpoint(whiteRemaining: 100, whiteMovesCompleted: 2);

        var beforeMove = PlayableMatchClockKernel.Snapshot(timeControl, checkpoint, Origin);
        var completed = PlayableMatchClockKernel.CompleteMove(
            timeControl,
            checkpoint,
            'w',
            Origin + TimeSpan.FromSeconds(2));

        beforeMove.ActiveStageIndex.Should().Be(2);
        beforeMove.DelayRemaining.Should().Be(TimeSpan.FromSeconds(1));
        completed.WhiteRemaining.Should().Be(TimeSpan.FromSeconds(130));
        completed.WhiteMovesCompleted.Should().Be(3);
    }

    [Fact]
    public void Snapshot_WhenNowPrecedesTurnStart_ShouldClampElapsedToZero()
    {
        var timeControl = new PlayableMatchTimeControl(
            TimeSpan.FromSeconds(30),
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2));
        var checkpoint = CreateCheckpoint(turnStartedAtUtc: Origin + TimeSpan.FromSeconds(5));

        var snapshot = PlayableMatchClockKernel.Snapshot(timeControl, checkpoint, Origin);

        snapshot.WhiteRemaining.Should().Be(TimeSpan.FromSeconds(30));
        snapshot.DelayRemaining.Should().Be(TimeSpan.FromSeconds(2));
        snapshot.SnapshotUtc.Should().Be(Origin);
    }

    [Fact]
    public void Snapshot_WhenPaused_ShouldFreezeValuesAndUseRequestedSnapshotTime()
    {
        var timeControl = new PlayableMatchTimeControl(
            TimeSpan.FromSeconds(30),
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2));
        var checkpoint = CreateCheckpoint(pausedAtUtc: Origin + TimeSpan.FromSeconds(3));
        var requestedSnapshot = Origin + TimeSpan.FromSeconds(10);

        var snapshot = PlayableMatchClockKernel.Snapshot(timeControl, checkpoint, requestedSnapshot);

        snapshot.WhiteRemaining.Should().Be(TimeSpan.FromSeconds(29));
        snapshot.DelayRemaining.Should().Be(TimeSpan.Zero);
        snapshot.IsPaused.Should().BeTrue();
        snapshot.SnapshotUtc.Should().Be(requestedSnapshot);
    }

    [Fact]
    public void Restore_WhenDelayExceedsCurrentStage_ShouldThrow()
    {
        var timeControl = new PlayableMatchTimeControl(
            TimeSpan.FromSeconds(30),
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2));
        var restore = new PlayableMatchClockRestore(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30),
            'w',
            TimeSpan.FromSeconds(3),
            false,
            0,
            0,
            0,
            Origin);

        var act = () => PlayableMatchClockKernel.Restore(timeControl, 'w', restore);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("restore");
    }

    [Fact]
    public void Restore_WhenStageIndexDoesNotMatchMoveCount_ShouldThrow()
    {
        var timeControl = new PlayableMatchTimeControl(
            TimeSpan.FromSeconds(30),
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2));
        var restore = new PlayableMatchClockRestore(
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30),
            'w',
            TimeSpan.FromSeconds(2),
            false,
            0,
            0,
            1,
            Origin);

        var act = () => PlayableMatchClockKernel.Restore(timeControl, 'w', restore);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("restore");
    }

    [Fact]
    public void SnapshotAtTurnStart_WhenCheckpointIsPaused_ShouldProjectFullDelayWithoutElapsedTime()
    {
        var timeControl = new PlayableMatchTimeControl(
            TimeSpan.FromSeconds(30),
            TimeSpan.Zero,
            TimeSpan.FromSeconds(4));
        var checkpoint = CreateCheckpoint(
            whiteRemaining: 25,
            blackRemaining: 20,
            activeColor: 'b',
            turnStartedAtUtc: Origin + TimeSpan.FromSeconds(5),
            pausedAtUtc: Origin + TimeSpan.FromSeconds(8));

        var snapshot = PlayableMatchClockKernel.SnapshotAtTurnStart(timeControl, checkpoint);

        snapshot.WhiteRemaining.Should().Be(TimeSpan.FromSeconds(25));
        snapshot.BlackRemaining.Should().Be(TimeSpan.FromSeconds(20));
        snapshot.ActiveColor.Should().Be('b');
        snapshot.DelayRemaining.Should().Be(TimeSpan.FromSeconds(4));
        snapshot.IsPaused.Should().BeTrue();
        snapshot.SnapshotUtc.Should().Be(Origin + TimeSpan.FromSeconds(5));
    }

    private static PlayableMatchClockCheckpoint CreateCheckpoint(
        int whiteRemaining = 30,
        int blackRemaining = 30,
        char activeColor = 'w',
        int whiteMovesCompleted = 0,
        int blackMovesCompleted = 0,
        DateTimeOffset? turnStartedAtUtc = null,
        DateTimeOffset? pausedAtUtc = null) =>
        new(
            TimeSpan.FromSeconds(whiteRemaining),
            TimeSpan.FromSeconds(blackRemaining),
            activeColor,
            whiteMovesCompleted,
            blackMovesCompleted,
            turnStartedAtUtc ?? Origin,
            pausedAtUtc);
}
