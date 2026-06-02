using Bezoro.Chess.UCI.API;
using Bezoro.Chess.UCI.API.Common.Enums;
using Bezoro.Chess.UCI.API.Types;
using Bezoro.Chess.UCI.Protocol.API.Types;
using Bezoro.Chess.UCI.Tests.Attributes;
using Bezoro.Chess.UCI.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Tests.API;

[TestSubject(typeof(UciGameEngineSession))]
[Trait("Category", "Integration")]
[Collection("Stockfish")]
public sealed class UciGameEngineSessionClockRestoreTests
{
	[IntegrationTest]
	public async Task UpdatePositionAsync_WhenClockRestoreIsProvided_ShouldExposeRestoredClock()
	{
		var options = UciCoordinatorOptions.Default with
		{
			TimeControl = new(
				TimeSpan.FromMinutes(5),
				TimeSpan.FromSeconds(2),
				TimeSpan.FromSeconds(3),
				PlayableMatchTimeoutPolicy.AutomaticLoss)
		};

		await using var coordinator = await UciGameEngineSession.CreateAsync(
			TestResourcePaths.STOCKFISH_PATH,
			options: options,
			ct: CancellationToken.None);

		var restore = new PlayableMatchClockRestore(
			TimeSpan.FromSeconds(135),
			TimeSpan.FromSeconds(100),
			'w',
			TimeSpan.FromSeconds(1),
			true,
			1,
			1,
			0,
			DateTimeOffset.UtcNow);

		await coordinator.UpdatePositionAsync(Fen.Default, ["e2e4", "e7e5"], restore, CancellationToken.None);

		coordinator.State.Clock.Should().NotBeNull();
		coordinator.State.Clock!.Value.WhiteRemaining.Should().Be(TimeSpan.FromSeconds(135));
		coordinator.State.Clock.Value.BlackRemaining.Should().Be(TimeSpan.FromSeconds(100));
		coordinator.State.Clock.Value.ActiveColor.Should().Be('w');
		coordinator.State.Clock.Value.IsPaused.Should().BeTrue();
	}

	[IntegrationTest]
	public async Task UpdatePositionAsync_WhenClockRestoreActiveColorDoesNotMatchPosition_ShouldThrow()
	{
		var options = UciCoordinatorOptions.Default with
		{
			TimeControl = new(
				TimeSpan.FromMinutes(5),
				TimeSpan.Zero,
				TimeSpan.Zero,
				PlayableMatchTimeoutPolicy.AutomaticLoss)
		};

		await using var coordinator = await UciGameEngineSession.CreateAsync(
			TestResourcePaths.STOCKFISH_PATH,
			options: options,
			ct: CancellationToken.None);

		var restore = new PlayableMatchClockRestore(
			TimeSpan.FromMinutes(4),
			TimeSpan.FromMinutes(4),
			'b',
			TimeSpan.Zero,
			false,
			0,
			0,
			0,
			DateTimeOffset.UtcNow);

		await Assert.ThrowsAsync<ArgumentException>(
			() => coordinator.UpdatePositionAsync(Fen.Default, null, restore, CancellationToken.None));
	}
}
