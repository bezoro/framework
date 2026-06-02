using Bezoro.Chess.UCI.API;
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
public sealed class UciGameEngineSessionLoadMatchTests
{
	[IntegrationTest]
	public async Task LoadMatchAsync_WhenSetupIncludesPlayedMovesAndClock_ShouldRestoreCompleteMatchState()
	{
		var options = UciCoordinatorOptions.Default with
		{
			TimeControl = new(
				TimeSpan.FromMinutes(5),
				TimeSpan.FromSeconds(2),
				TimeSpan.FromSeconds(3),
				PlayableMatchTimeoutPolicy.AutomaticLoss,
				[
					new(
						1,
						TimeSpan.Zero,
						TimeSpan.FromSeconds(4),
						TimeSpan.FromSeconds(5))
				])
		};
		var setup = new PlayableMatchSetup(
			Fen.Default,
			["e2e4", "e7e5"],
			new PlayableMatchClockSetup(
				TimeSpan.FromSeconds(135),
				TimeSpan.FromSeconds(100),
				TimeSpan.FromSeconds(4),
				isPaused: true));

		await using var session = await UciGameEngineSession.CreateAsync(
			TestResourcePaths.STOCKFISH_PATH,
			options: options,
			ct: CancellationToken.None);

		var state = await session.LoadMatchAsync(setup, CancellationToken.None);

		state.BaseFen.Should().Be(Fen.Default);
		state.CurrentFen.ActiveColor.Should().Be('w');
		state.PlayedMoves.Should().Equal(["e2e4", "e7e5"]);
		state.Clock.Should().NotBeNull();
		state.Clock!.Value.WhiteRemaining.Should().Be(TimeSpan.FromSeconds(135));
		state.Clock.Value.BlackRemaining.Should().Be(TimeSpan.FromSeconds(100));
		state.Clock.Value.ActiveColor.Should().Be('w');
		state.Clock.Value.ActiveStageIndex.Should().Be(1);
		state.Clock.Value.DelayRemaining.Should().Be(TimeSpan.FromSeconds(4));
		state.Clock.Value.IsPaused.Should().BeTrue();
	}

	[IntegrationTest]
	public async Task PlayControlledMoveIfNeededAsync_WhenCurrentSideIsManual_ShouldReturnNull()
	{
		await using var session = await UciGameEngineSession.CreateAsync(
			TestResourcePaths.STOCKFISH_PATH,
			whiteController: MatchSideControllerKind.Manual,
			blackController: MatchSideControllerKind.Engine,
			ct: CancellationToken.None);

		await session.LoadMatchAsync(PlayableMatchSetup.Standard, CancellationToken.None);

		var result = await session.PlayControlledMoveIfNeededAsync(CancellationToken.None);

		result.Should().BeNull();
		session.State.PlayedMoves.Should().BeEmpty();
	}
}
