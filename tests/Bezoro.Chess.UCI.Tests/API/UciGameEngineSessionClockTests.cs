using Bezoro.Chess.UCI.API;
using Bezoro.Chess.UCI.API.Common.Enums;
using Bezoro.Chess.UCI.API.Types;
using Bezoro.Chess.UCI.Protocol;
using Bezoro.Chess.UCI.Protocol.Internal;
using Bezoro.Chess.UCI.Tests.Attributes;
using Bezoro.Chess.UCI.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Tests.API;

[TestSubject(typeof(UciGameEngineSession))]
[Trait("Category", "Integration")]
[Collection("Stockfish")]
public sealed class UciGameEngineSessionClockTests
{
	private static readonly PlayableMatchTimeControl TimeControl = new(
		TimeSpan.FromSeconds(30),
		TimeSpan.FromSeconds(1),
		TimeSpan.FromSeconds(2),
		PlayableMatchTimeoutPolicy.AutomaticLoss);

	[IntegrationTest]
	public async Task MakeMoveAsync_WhenClockTimeIsInjected_ShouldApplyExactDelayDebitAndIncrement()
	{
		var now = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
		await using var session = CreateSession(() => now);
		await session.StartAsync();
		await session.LoadMatchAsync(PlayableMatchSetup.Standard);

		now += TimeSpan.FromSeconds(5);
		var state = await session.MakeMoveAsync("e2e4");

		state.Clock.Should().NotBeNull();
		state.Clock!.Value.WhiteRemaining.Should().Be(TimeSpan.FromSeconds(28));
		state.Clock.Value.BlackRemaining.Should().Be(TimeSpan.FromSeconds(30));
		state.Clock.Value.ActiveColor.Should().Be('b');
		state.Clock.Value.DelayRemaining.Should().Be(TimeSpan.FromSeconds(2));
		state.Clock.Value.SnapshotUtc.Should().Be(now);
	}

	[IntegrationTest]
	public async Task UndoAsync_WhenClockWasDebited_ShouldResetAtInjectedUndoTime()
	{
		var now = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
		await using var session = CreateSession(() => now);
		await session.StartAsync();
		await session.LoadMatchAsync(PlayableMatchSetup.Standard);

		now += TimeSpan.FromSeconds(5);
		await session.MakeMoveAsync("e2e4");
		now += TimeSpan.FromSeconds(7);
		var state = await session.UndoAsync();

		state.Clock.Should().NotBeNull();
		state.Clock!.Value.WhiteRemaining.Should().Be(TimeSpan.FromSeconds(30));
		state.Clock.Value.BlackRemaining.Should().Be(TimeSpan.FromSeconds(30));
		state.Clock.Value.ActiveColor.Should().Be('w');
		state.Clock.Value.DelayRemaining.Should().Be(TimeSpan.FromSeconds(2));
		state.Clock.Value.SnapshotUtc.Should().Be(now);
	}

	private static UciGameEngineSession CreateSession(Func<DateTimeOffset> utcNowProvider)
	{
		var snapshotClient = new UciEngineClient(TestResourcePaths.STOCKFISH_PATH);
		var ponder = new UciPonderRuntime(new UciEngineClient(TestResourcePaths.STOCKFISH_PATH));
		var classificationClient = new UciEngineClient(TestResourcePaths.STOCKFISH_PATH);
		var options = UciCoordinatorOptions.Default with { TimeControl = TimeControl };
		return new(snapshotClient, ponder, classificationClient, options: options, utcNowProvider: utcNowProvider);
	}
}
