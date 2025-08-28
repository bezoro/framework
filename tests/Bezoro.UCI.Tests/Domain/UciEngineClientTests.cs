using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain;
using Bezoro.UCI.Tests._Resources;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientTests
{
	[Fact]
	public async Task BestMoveReceived_Fires_And_Provides_BestMove()
	{
		var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH);
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
	public async Task GetFenViaDAsync_InCheckPosition_Emits_Checkers_Line()
	{
		var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH);
		var engine    = new UciEngineClient(transport);
		await engine.StartAsync();

		// Black to move and in check: White queen on h7 attacks king on h8.
		var fen = Fen.Parse("7k/7Q/7K/8/8/8/8/8 b - - 0 1");
		await engine.SetPositionAsync(fen!.Value, null, CancellationToken.None);

		var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

		void Handler(string line)
		{
			if (line.TrimStart().StartsWith("checkers", StringComparison.OrdinalIgnoreCase))
				tcs.TrySetResult(line);
		}

		engine.LineReceived += Handler;
		try
		{
			// Trigger engine to dump board state, which includes a "checkers" line when in check.
			await engine.GetFenViaDAsync(CancellationToken.None);

			bool received = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5))) == tcs.Task;
			received.Should().BeTrue(
				"engine should emit a 'checkers' line for positions where the side to move is in check");

			string checkersLine = tcs.Task.Result;
			checkersLine.Should().StartWithEquivalentOf("checkers");
		}
		finally
		{
			engine.LineReceived -= Handler;
		}
	}

	[Fact]
	public async Task GetFenViaDAsync_Returns_CompleteFenObject()
	{
		var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH);
		var engine    = new UciEngineClient(transport);
		await engine.StartAsync();

		// Use a known valid position to ensure deterministic behavior
		await engine.SetPositionAsync(Fen.Default, null, CancellationToken.None);

		var fen = await engine.GetFenViaDAsync(CancellationToken.None);

		fen.HasValue.Should().BeTrue();
		var f = fen!.Value;

		// Raw and validation
		f.Raw.Should().NotBeNullOrWhiteSpace();
		Fen.Validate(f.Raw).Should().BeTrue();

		// Implicit cast and ToString semantics
		string rawCast = f;
		rawCast.Should().Be(f.Raw);
		f.ToString().Should().Be(f.Raw);

		// Core parts
		f.PiecePlacement.Should().NotBeNullOrWhiteSpace();
		f.PiecePlacement.Should().Contain("/"); // typical FEN ranks

		(f.ActiveColor == 'w' || f.ActiveColor == 'b').Should().BeTrue();

		f.CastlingRights.Should().NotBeNull();  // may be "-" or castling flags
		f.EnPassantTarget.Should().NotBeNull(); // may be "-" or a square

		f.FenParts.Should().NotBeNull();
		f.FenParts.Length.Should().BeGreaterOrEqualTo(6);

		f.HalfmoveClock.Should().BeGreaterOrEqualTo(0);
		f.FullmoveNumber.Should().BeGreaterOrEqualTo(1);

		// Checkers may be empty depending on engine output, but property should be non-null
		f.Checkers.Should().NotBeNull();
	}

	[Fact]
	public async Task GetLegalMovesViaGoPerft1Async_ContainsCommonOpeners()
	{
		var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH);
		var engine    = new UciEngineClient(transport);
		await engine.StartAsync();
		await engine.SetPositionAsync(Fen.Default, null, CancellationToken.None);

		var legalMoves = await engine.GetLegalMovesViaGoPerft1Async(CancellationToken.None);

		legalMoves.Should().Contain(new[] { "e2e4", "d2d4", "g1f3", "c2c4" });
	}

	[Fact]
	public async Task GetLegalMovesViaGoPerft1Async_InStalematePosition_ReturnsNoMoves()
	{
		var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH);
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
		var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH);
		var engine    = new UciEngineClient(transport);
		await engine.StartAsync();
		await engine.SetPositionAsync(Fen.Default, null, CancellationToken.None);

		var legalMoves = await engine.GetLegalMovesViaGoPerft1Async(CancellationToken.None);

		legalMoves.Should().NotBeNull();
	}

	[Fact]
	public async Task GoAsync_WhenMateInOnePosition_ReturnsHasMateAndMateScore()
	{
		var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH);
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
		var  transport     = new ProcessUciTransport(TestConsts.STOCKFISH_PATH);
		var  engine        = new UciEngineClient(transport);
		await engine.StartAsync();

		var searchResult = await engine.GoAsync(new() { Depth = expectedDepth }, CancellationToken.None);

		searchResult.Should().NotBeNull();
		searchResult.ReachedDepth.Should().Be(expectedDepth);
	}

	[Fact]
	public async Task GoAsync_WithSearchMove_SearchesWithExpectedSearchMove()
	{
		var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH);
		var engine    = new UciEngineClient(transport);
		await engine.StartAsync();
		await engine.SetPositionAsync(Fen.Default, null, CancellationToken.None);

		var searchResult = await engine.GoAsync(new() { SearchMoves = ["a2a4"] }, CancellationToken.None);

		searchResult.Should().NotBeNull();
		searchResult.PrincipalVariations.Count.Should().BeGreaterThan(0);
	}

	[Fact]
	public void BuildGoCommand_MoveTime_TokenIncluded()
	{
		string cmd = UciEngineClient.BuildGoCommand(new() { MoveTimeMs = 1500 });
		cmd.Should().Be("go movetime 1500");
	}

	[Fact]
	public void BuildGoCommand_NodesDepthMateInfinitePonder_AllIncluded()
	{
		string cmd = UciEngineClient.BuildGoCommand(
			new()
			{
				Nodes    = 1_000_000,
				Depth    = 12,
				Mate     = 3,
				Infinite = true,
				Ponder   = true
			});

		cmd.Should().StartWith("go ");
		cmd.Should().Contain("ponder");
		cmd.Should().Contain("infinite");
		cmd.Should().Contain("nodes 1000000");
		cmd.Should().Contain("depth 12");
		cmd.Should().Contain("mate 3");
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
	public void BuildGoCommand_TimeControls_IncludeExpectedTokens()
	{
		string cmd = UciEngineClient.BuildGoCommand(
			new()
			{
				WhiteTimeMs      = 10_000,
				BlackTimeMs      = 20_000,
				WhiteIncrementMs = 100,
				BlackIncrementMs = 200
			});

		cmd.Should().StartWith("go ");
		cmd.Should().Contain("wtime 10000");
		cmd.Should().Contain("btime 20000");
		cmd.Should().Contain("winc 100");
		cmd.Should().Contain("binc 200");
		cmd.Should().NotContain("depth 6"); // since limits are present
	}

	[Fact]
	public void BuildGoCommand_WhenNoLimits_AddsDefaultDepth6()
	{
		string cmd = UciEngineClient.BuildGoCommand(new());
		cmd.Should().Be("go depth 6");
	}

	[Fact]
	public void BuildGoCommand_WhenOnlySearchMoves_DefaultDepthStillAdded()
	{
		string cmd = UciEngineClient.BuildGoCommand(
			new()
			{
				SearchMoves = new[] { "e2e4", "B1C3" }
			});

		cmd.Should().Contain("depth 6");
		cmd.Should().Contain("searchmoves e2e4 b1c3");
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
