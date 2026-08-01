using Bezoro.Chess.UCI.Protocol.Tests.Domain;
using FluentAssertions;
using JetBrains.Annotations;
using NSubstitute;

namespace Bezoro.Chess.UCI.Protocol.Tests.API;

[TestSubject(typeof(UciPlayableMatchSession))]
public sealed class UciPlayableMatchSessionClockTests
{
	private static readonly PlayableMatchTimeControl TimeControl = new(
		TimeSpan.FromSeconds(30),
		TimeSpan.FromSeconds(1),
		TimeSpan.FromSeconds(2),
		PlayableMatchTimeoutPolicy.AutomaticLoss);

	[Fact]
	public async Task ApplyMove_WhenClockIsPaused_ShouldStartNextTurnPausedAndFrozen()
	{
		var now = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
		await using var playingClient = await CreateStartedClientAsync();
		await using var analysisClient = await CreateStartedClientAsync();
		await using var moveListClient = await CreateStartedClientAsync();
		var session = CreateSession(playingClient, analysisClient, moveListClient, () => now);
		await session.StartNewGameAsync();
		await session.RefreshAsync();

		now += TimeSpan.FromSeconds(5);
		session.PauseClock();
		now += TimeSpan.FromSeconds(10);
		session.ApplyMove("e2e4");
		now += TimeSpan.FromSeconds(10);
		var state = await session.RefreshAsync();

		state.Clock.Should().NotBeNull();
		state.Clock!.Value.WhiteRemaining.Should().Be(TimeSpan.FromSeconds(28));
		state.Clock.Value.BlackRemaining.Should().Be(TimeSpan.FromSeconds(30));
		state.Clock.Value.ActiveColor.Should().Be('b');
		state.Clock.Value.DelayRemaining.Should().Be(TimeSpan.FromSeconds(2));
		state.Clock.Value.IsPaused.Should().BeTrue();
		state.Clock.Value.SnapshotUtc.Should().Be(now);
		session.CancelAnalysis();
	}

	private static UciPlayableMatchSession CreateSession(
		UciEngineClient       playingClient,
		UciEngineClient       analysisClient,
		UciEngineClient       moveListClient,
		Func<DateTimeOffset>  utcNowProvider) =>
		new(
			playingClient,
			analysisClient,
			moveListClient,
			perspectiveColor: 'w',
			whiteController: MatchSideControllerKind.Manual,
			blackController: MatchSideControllerKind.Manual,
			engineMoveTimeMs: 100,
			moveListAnalysisTimeMs: 10,
			moveListFallbackTimeMs: 10,
			timeControl: TimeControl,
			utcNowProvider: utcNowProvider);

	private static async Task<UciEngineClient> CreateStartedClientAsync()
	{
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		transport.When(x => x.WriteLineAsync("isready", Arg.Any<CancellationToken>()))
			.Do(_ => channel.Writer.TryWrite("readyok"));
		return await UciEngineClientTestHelpers.StartClientWithHandshakeAsync(transport, channel);
	}
}
