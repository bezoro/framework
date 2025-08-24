using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests;

[TestSubject(typeof(UciEngineClient))]
[Trait("Category", "Integration")]
public class UciEngineClientTests
{
	public const string STOCKFISH_PATH = "Engine/stockfish/stockfish-windows-x86-64-avx2.exe";

	[Fact]
	public async Task BestMoveReceived_Fires_And_Provides_BestMove()
	{
		var transport = new ProcessUciTransport(STOCKFISH_PATH);
		var engine    = new UciEngineClient(transport);
		await engine.StartAsync();
		await engine.SetPositionAsync(Fen.Default, null, CancellationToken.None);

		string?   best   = null;
		string?   ponder = null;
		using var _      = new CancellationTokenSource(TimeSpan.FromSeconds(10));

		var evt = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		engine.BestMoveReceived += (b, p) =>
		{
			best   = b;
			ponder = p;
			evt.TrySetResult(true);
		};

		var _result = await engine.GoAsync(new() { Depth = 6 }, CancellationToken.None);
		await evt.Task; // ensure event fired

		best.Should().NotBeNullOrWhiteSpace();
		UciEngineClient.IsUciMoveString(best!).Should().BeTrue();
		if (!string.IsNullOrWhiteSpace(ponder))
			UciEngineClient.IsUciMoveString(ponder!).Should().BeTrue();
	}

	[Fact]
	public async Task GetLegalMovesViaGoPerft1Async_ContainsCommonOpeners()
	{
		var transport = new ProcessUciTransport(STOCKFISH_PATH);
		var engine    = new UciEngineClient(transport);
		await engine.StartAsync();
		await engine.SetPositionAsync(Fen.Default, null, CancellationToken.None);

		var legalMoves = await engine.GetLegalMovesViaGoPerft1Async(CancellationToken.None);

		legalMoves.Should().Contain(new[] { "e2e4", "d2d4", "g1f3", "c2c4" });
	}

	[Fact]
	public async Task GetLegalMovesViaGoPerft1Async_InStalematePosition_ReturnsNoMoves()
	{
		var transport = new ProcessUciTransport(STOCKFISH_PATH);
		var engine    = new UciEngineClient(transport);
		await engine.StartAsync();

		// From this position, the move b7b6 results in stalemate for Black.
		var fen = Fen.Parse("k7/1QK5/8/8/8/8/8/8 w - - 0 1");
		await engine.SetPositionAsync(fen!.Value, new[] { "b7b6" }, CancellationToken.None);

		var legalMoves = await engine.GetLegalMovesViaGoPerft1Async(CancellationToken.None);

		legalMoves.Should().NotBeNull();
		legalMoves.Count.Should().Be(0);
	}

	[Fact]
	public async Task GetLegalMovesViaGoPerft1Async_WhenCalled_ReturnsLegalMoves()
	{
		var transport = new ProcessUciTransport(STOCKFISH_PATH);
		var engine    = new UciEngineClient(transport);
		await engine.StartAsync();
		await engine.SetPositionAsync(Fen.Default, null, CancellationToken.None);

		var legalMoves = await engine.GetLegalMovesViaGoPerft1Async(CancellationToken.None);

		legalMoves.Should().NotBeNull();
	}

	[Fact]
	public async Task GoAsync_WhenMateInOnePosition_ReturnsHasMateAndMateScore()
	{
		var transport = new ProcessUciTransport(STOCKFISH_PATH);
		var engine    = new UciEngineClient(transport);
		await engine.StartAsync();

		// Position: Black king on h8, White queen on f7, White king on h6 (white to move). f7g7 is mate.
		var fen = Fen.Parse("7k/5Q2/7K/8/8/8/8/8 w - - 0 1");
		await engine.SetPositionAsync(fen!.Value, null, CancellationToken.None);

		var result = await engine.GoAsync(new() { Depth = 10 }, CancellationToken.None);

		result.Should().NotBeNull();
		result.HasMate.Should().BeTrue();
		result.MateScore.HasValue.Should().BeTrue();
	}

	[Fact]
	public async Task GoAsync_WithDepth_SearchesWithExpectedDepth()
	{
		uint expectedDepth = 6;
		var  transport     = new ProcessUciTransport(STOCKFISH_PATH);
		var  engine        = new UciEngineClient(transport);
		await engine.StartAsync();

		var searchResult = await engine.GoAsync(new() { Depth = expectedDepth }, CancellationToken.None);

		searchResult.Should().NotBeNull();
		searchResult.ReachedDepth.Should().Be(expectedDepth);
	}

	[Fact]
	public async Task GoAsync_WithSearchMove_SearchesWithExpectedSearchMove()
	{
		var transport = new ProcessUciTransport(STOCKFISH_PATH);
		var engine    = new UciEngineClient(transport);
		await engine.StartAsync();
		await engine.SetPositionAsync(Fen.Default, null, CancellationToken.None);

		var searchResult = await engine.GoAsync(new() { SearchMoves = ["a2a4"] }, CancellationToken.None);

		searchResult.Should().NotBeNull();
		searchResult.PrincipalVariations.Count.Should().BeGreaterThan(0);
	}

	[Fact]
	[Trait("Category", "Unit")]
	public void BuildGoCommand_SearchMoves_FiltersAndLowercases()
	{
		string cmd = UciEngineClient.BuildGoCommand(
			new()
			{
				SearchMoves = new[] { "A2A4", "h7h8Q", "z9z9", "", "   ", "B1C3" }
			});

		cmd.Should().Contain("searchmoves a2a4 h7h8q b1c3");
		cmd.Should().NotContain("z9z9");
	}

	[Fact]
	[Trait("Category", "Unit")]
	public void IsUciMoveString_ValidAndInvalid()
	{
		// Valid basic moves
		UciEngineClient.IsUciMoveString("e2e4").Should().BeTrue();
		UciEngineClient.IsUciMoveString("a7a8q").Should().BeTrue();
		UciEngineClient.IsUciMoveString("H7H8Q").Should().BeTrue();
		UciEngineClient.IsUciMoveString("b1c3").Should().BeTrue();

		// Invalid shapes
		UciEngineClient.IsUciMoveString("e9e1").Should().BeFalse();
		UciEngineClient.IsUciMoveString("i2e4").Should().BeFalse();
		UciEngineClient.IsUciMoveString("e2e").Should().BeFalse();
		UciEngineClient.IsUciMoveString("e2e4qq").Should().BeFalse();
		UciEngineClient.IsUciMoveString("e2e4x").Should().BeFalse();
	}
}
