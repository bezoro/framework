using System.Collections.Generic;
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using FluentAssertions;

namespace Bezoro.Chess.UCI.Protocol.Tests.API;

public sealed class UciPlayableLocalMatchTests
{
    [Fact]
    public async Task LocalSession_PlaysAndUndoesWithoutAnyEngineClient()
    {
        var match = new UciPlayableMatchSession();
        match.HasEngineClients.Should().BeFalse();
        await match.StartNewGameAsync();
        match.Refresh().LegalMoves.Should().HaveCount(20);
        match.ApplyMove("e2e4");
        match.Refresh().Fen.ActiveColor.Should().Be('b');
        match.ApplyMove("e7e5");
        var state = match.Refresh();
        state.MoveHistory.Should().HaveCount(2);
        match.UndoMoves();
        match.Refresh().Fen.Should().Be(Fen.Default.ApplyMove("e2e4"));
        match.TryGetPositionAnalysis(state.PositionKey, out _).Should().BeFalse();
    }

    [Fact]
    public async Task LocalSession_AdjudicatesCheckmateWithOrderedEvents()
    {
        var match = new UciPlayableMatchSession();
        await match.LoadMatchAsync(new(Fen.Parse("7k/5Q2/7K/8/8/8/8/8 w - - 0 1")!.Value));
        var events = new List<PlayableMatchEventKind>();
        match.EventOccurred += e => events.Add(e.Kind);

        match.ApplyMove("f7g7");
        match.Refresh().Result.Reason.Should().Be(PlayableMatchResultReason.Checkmate);
        events.Should().ContainInOrder(PlayableMatchEventKind.MoveApplied, PlayableMatchEventKind.ResultChanged);
    }

    [Fact]
    public async Task LocalSession_ExplicitEngineAnalysisHasClearFailure()
    {
        var match = new UciPlayableMatchSession();
        await match.StartNewGameAsync();
        match.Refresh();
        await match.Invoking(s => s.GetLegalMoveAnalysisAsync()).Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*analysis engine*");
    }

    [Fact]
    public async Task LocalSession_ClassifiesMovesOnlyWhenRequested()
    {
        var match = new UciPlayableMatchSession();
        await match.StartNewGameAsync();
        match.Refresh();
        match.GetCurrentLegalMoveClassifications().Should().BeEmpty();

        var classifications = await match.WaitForCurrentMoveClassificationsAsync();
        classifications.Should().HaveCount(20);
        classifications["e2e4"].MovingPiece.Should().Be('P');
        match.CancelAnalysis();
    }
}
