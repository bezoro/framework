using System.Collections.Concurrent;
using System.Threading.Channels;
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using Bezoro.Chess.UCI.Protocol.Tests.Domain;
using FluentAssertions;
using NSubstitute;

namespace Bezoro.Chess.UCI.Protocol.Tests.API;

public sealed class UciGameReviewParallelTests
{
	[Fact]
	public async Task AnalyzeAsync_WithTwoWorkers_ShouldSearchConcurrentlyAndReturnChronologicalResults()
	{
		var first = new ControlledEngine();
		var second = new ControlledEngine();
		await using var firstClient = first.Client;
		await using var secondClient = second.Client;
		await Task.WhenAll(firstClient.StartAsync(), secondClient.StartAsync());
		var clients = new List<UciEngineClient> { firstClient, secondClient };
		var analyzer = new UciGameReviewAnalyzer(clients, Options(parallelism: 8));
		clients.Clear(); // Construction snapshots the pool.
		var progress = new PositionProgress();

		var reviewTask = analyzer.AnalyzeAsync(Fen.Default, ["e2e4", "e7e5", "g1f3"], progress);
		await Task.WhenAll(first.SearchStarted, second.SearchStarted).WaitAsync(TimeSpan.FromSeconds(5));
		first.ActiveSearches.Should().Be(1);
		second.ActiveSearches.Should().Be(1);
		first.AutoComplete = true;
		second.AutoComplete = true;
		second.CompletePending();
		await progress.FirstReport.WaitAsync(TimeSpan.FromSeconds(5));
		progress.Snapshot[0].PositionIndex.Should().Be(1);
		reviewTask.IsCompleted.Should().BeFalse();
		first.CompletePending();
		var review = await reviewTask.WaitAsync(TimeSpan.FromSeconds(5));

		review.Positions.Select(evaluation => evaluation.Position.Raw).Should().Equal(
			Fen.Default.Raw,
			Fen.Default.ApplyMove("e2e4").Raw,
			Fen.Default.ApplyMove("e2e4").ApplyMove("e7e5").Raw,
			Fen.Default.ApplyMove("e2e4").ApplyMove("e7e5").ApplyMove("g1f3").Raw);
		review.Moves.Select(move => move.Move).Should().Equal("e2e4", "e7e5", "g1f3");
		first.SearchCount.Should().BeGreaterThan(0);
		second.SearchCount.Should().BeGreaterThan(0);
		(first.SearchCount + second.SearchCount).Should().Be(4);
		first.MaxActiveSearches.Should().Be(1);
		second.MaxActiveSearches.Should().Be(1);
		progress.Snapshot.Select(value => value.CompletedPositions).Should().Equal(1, 2, 3, 4);
		progress.Snapshot.Select(value => value.PositionIndex).Should().BeEquivalentTo(new[] { 0, 1, 2, 3 });
		progress.ConcurrentCallbacks.Should().BeFalse();
		var commands = first.Commands.Concat(second.Commands).ToArray();
		commands.Should().Contain("position startpos moves e2e4 e7e5 g1f3");
		commands.Count(command => command == "setoption name Threads value 1").Should().Be(2);
		commands.Count(command => command == "setoption name Hash value 16").Should().Be(2);
	}

	[Fact]
	public async Task AnalyzeAsync_WithDefaultOptions_ShouldUseOnlyFirstPoolClient()
	{
		var first = new ControlledEngine { AutoComplete = true };
		var second = new ControlledEngine();
		await using var firstClient = first.Client;
		await using var secondClient = second.Client;
		await Task.WhenAll(firstClient.StartAsync(), secondClient.StartAsync());
		var analyzer = new UciGameReviewAnalyzer(new[] { firstClient, secondClient }, Options(parallelism: 1));

		var review = await analyzer.AnalyzeAsync(Fen.Default, ["e2e4"]);

		review.Positions.Length.Should().Be(2);
		first.SearchCount.Should().Be(2);
		second.SearchCount.Should().Be(0);
		second.Commands.Should().NotContain(command => command.StartsWith("setoption ", StringComparison.Ordinal));
	}

	[Fact]
	public async Task AnalyzeAsync_WhenOneWorkerFails_ShouldCancelAndDrainOtherWorkerBeforeReusingPool()
	{
		var first = new ControlledEngine();
		var second = new ControlledEngine();
		await using var firstClient = first.Client;
		await using var secondClient = second.Client;
		await Task.WhenAll(firstClient.StartAsync(), secondClient.StartAsync());
		var analyzer = new UciGameReviewAnalyzer(new[] { firstClient, secondClient }, Options(parallelism: 2));
		var failed = analyzer.AnalyzeAsync(Fen.Default, ["e2e4", "e7e5"]);
		await Task.WhenAll(first.SearchStarted, second.SearchStarted).WaitAsync(TimeSpan.FromSeconds(5));

		second.CompletePending("score cp 10 lowerbound wdl 400 200 400");
		await first.StopRequested.WaitAsync(TimeSpan.FromSeconds(5));
		await FluentActions.Awaiting(() => failed).Should().ThrowAsync<InvalidOperationException>();

		first.ActiveSearches.Should().Be(0);
		second.ActiveSearches.Should().Be(0);
		first.AutoComplete = true;
		second.AutoComplete = true;
		var recovered = await analyzer.AnalyzeAsync(Fen.Default, ["e2e4"]).WaitAsync(TimeSpan.FromSeconds(5));
		recovered.Moves.Should().ContainSingle();
	}

	[Fact]
	public async Task AnalyzeAsync_WhenCanceled_ShouldAwaitEveryEngineCleanupBeforeStartingNextReview()
	{
		var first = new ControlledEngine { CompleteOnStop = false };
		var second = new ControlledEngine { CompleteOnStop = false };
		await using var firstClient = first.Client;
		await using var secondClient = second.Client;
		await Task.WhenAll(firstClient.StartAsync(), secondClient.StartAsync());
		var analyzer = new UciGameReviewAnalyzer(new[] { firstClient, secondClient }, Options(parallelism: 2));
		using var cancellation = new CancellationTokenSource();
		var canceled = analyzer.AnalyzeAsync(Fen.Default, ["e2e4", "e7e5"], ct: cancellation.Token);
		await Task.WhenAll(first.SearchStarted, second.SearchStarted).WaitAsync(TimeSpan.FromSeconds(5));

		cancellation.Cancel();
		await Task.WhenAll(first.StopRequested, second.StopRequested).WaitAsync(TimeSpan.FromSeconds(5));
		canceled.IsCompleted.Should().BeFalse();
		var next = analyzer.AnalyzeAsync(Fen.Default, ["e2e4"]);
		(first.SearchCount + second.SearchCount).Should().Be(2);
		first.AutoComplete = true;
		second.AutoComplete = true;
		first.CompletePending();
		second.CompletePending();
		await FluentActions.Awaiting(() => canceled).Should().ThrowAsync<OperationCanceledException>();
		var result = await next.WaitAsync(TimeSpan.FromSeconds(5));

		result.Moves.Should().ContainSingle();
		(first.SearchCount + second.SearchCount).Should().Be(4);
		first.MaxActiveSearches.Should().Be(1);
		second.MaxActiveSearches.Should().Be(1);
	}

	[Fact]
	public async Task AnalyzeAsync_WhenQueued_ShouldSnapshotMovesBeforeAwaitingItsTurn()
	{
		var engine = new ControlledEngine();
		await using var client = engine.Client;
		await client.StartAsync();
		var analyzer = new UciGameReviewAnalyzer(client, Options(parallelism: 1));
		var first = analyzer.AnalyzeAsync(Fen.Default, ["e2e4"]);
		await engine.SearchStarted.WaitAsync(TimeSpan.FromSeconds(5));
		var mutableMoves = new List<string> { "d2d4" };

		var queued = analyzer.AnalyzeAsync(Fen.Default, mutableMoves);
		mutableMoves[0] = "e2e4";
		engine.AutoComplete = true;
		engine.CompletePending();
		await first;
		var review = await queued.WaitAsync(TimeSpan.FromSeconds(5));

		review.Moves[0].Move.Should().Be("d2d4");
		engine.Commands.Should().Contain("position startpos moves d2d4");
	}

	[Fact]
	public async Task AnalyzeAsync_WhenHistoryExceedsBudget_ShouldRejectInsteadOfTruncatingOrSendingCommands()
	{
		var engine = new ControlledEngine();
		await using var client = engine.Client;
		var analyzer = new UciGameReviewAnalyzer(client,
			new GameReviewOptions(new() { Depth = 1 }) { MaxGameMoves = 1 });

		await FluentActions.Awaiting(() => analyzer.AnalyzeAsync(Fen.Default, ["e2e4", "e7e5"]))
			.Should().ThrowAsync<ArgumentException>().WithMessage("*MaxGameMoves*");

		engine.Commands.Should().BeEmpty();
	}

	[Fact]
	public async Task AnalyzeAsync_WhenAResourceBudgetExceedsAdvertisedRange_ShouldRejectBeforeReconfiguringAnyClient()
	{
		var first = new ControlledEngine { AutoComplete = true };
		var second = new ControlledEngine(maxThreads: 1) { AutoComplete = true };
		await using var firstClient = first.Client;
		await using var secondClient = second.Client;
		await Task.WhenAll(firstClient.StartAsync(), secondClient.StartAsync());
		var analyzer = new UciGameReviewAnalyzer(new[] { firstClient, secondClient },
			new GameReviewOptions(new() { Depth = 1 }) { MaxParallelPositions = 2, ThreadsPerEngine = 2 });

		await FluentActions.Awaiting(() => analyzer.AnalyzeAsync(Fen.Default, ["e2e4"]))
			.Should().ThrowAsync<ArgumentException>().WithMessage("*Threads*");

		first.SearchCount.Should().Be(0);
		second.SearchCount.Should().Be(0);
		first.Commands.Concat(second.Commands).Should().NotContain(command => command.StartsWith("setoption ", StringComparison.Ordinal));
	}

	[Fact]
	public async Task Constructor_ShouldRejectAnEmptyOrDuplicatedPool()
	{
		var engine = new ControlledEngine();
		await using var client = engine.Client;
		Action empty = () => new UciGameReviewAnalyzer(Array.Empty<UciEngineClient>(), Options(1));
		Action duplicated = () => new UciGameReviewAnalyzer(new[] { engine.Client, engine.Client }, Options(2));

		empty.Should().Throw<ArgumentException>();
		duplicated.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void GameReviewOptions_ShouldRejectNonpositiveResourceLimits()
	{
		Action parallelism = () => new GameReviewOptions(new() { Depth = 1 }) { MaxParallelPositions = 0 };
		Action threads = () => new GameReviewOptions(new() { Depth = 1 }) { ThreadsPerEngine = -1 };
		Action hash = () => new GameReviewOptions(new() { Depth = 1 }) { HashMegabytesPerEngine = 0 };
		Action history = () => new GameReviewOptions(new() { Depth = 1 }) { MaxGameMoves = 0 };

		parallelism.Should().Throw<ArgumentOutOfRangeException>();
		threads.Should().Throw<ArgumentOutOfRangeException>();
		hash.Should().Throw<ArgumentOutOfRangeException>();
		history.Should().Throw<ArgumentOutOfRangeException>();
	}

	private static GameReviewOptions Options(int parallelism) =>
		new(new() { Depth = 1 }) { MaxParallelPositions = parallelism };

	private sealed class PositionProgress : IProgress<GameReviewProgress>
	{
		private readonly ConcurrentQueue<GameReviewProgress> _values = new();
		private readonly TaskCompletionSource _firstReport = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private int _reporting;
		private int _concurrentCallbacks;
		internal GameReviewProgress[] Snapshot => _values.ToArray();
		internal Task FirstReport => _firstReport.Task;
		internal bool ConcurrentCallbacks => Volatile.Read(ref _concurrentCallbacks) != 0;
		public void Report(GameReviewProgress value)
		{
			if (Interlocked.Increment(ref _reporting) > 1) Interlocked.Exchange(ref _concurrentCallbacks, 1);
			try
			{
				_values.Enqueue(value);
				_firstReport.TrySetResult();
			}
			finally { Interlocked.Decrement(ref _reporting); }
		}
	}

	private sealed class ControlledEngine
	{
		private readonly Channel<string> _channel;
		private readonly ConcurrentQueue<string> _commands = new();
		private readonly object _sync = new();
		private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private string _position = string.Empty;
		private string? _pendingBestMove;
		private int _searches, _activeSearches, _maxActiveSearches;
		private volatile bool _autoComplete;

		internal ControlledEngine(int maxThreads = 16)
		{
			var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
			_channel = channel;
			transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
			{
				string command = (string)call[0];
				_commands.Enqueue(command);
				if (command == "uci")
				{
					_channel.Writer.TryWrite($"option name Threads type spin default 4 min 1 max {maxThreads}");
					_channel.Writer.TryWrite("option name Hash type spin default 128 min 1 max 1024");
					_channel.Writer.TryWrite("uciok");
				}
				if (command == "isready") _channel.Writer.TryWrite("readyok");
				if (command.StartsWith("position ", StringComparison.Ordinal))
				{
					lock (_sync)
					{
						if (_pendingBestMove is not null) throw new InvalidOperationException("Client position changed during an active search.");
						_position = command;
					}
				}
				if (command.StartsWith("go ", StringComparison.Ordinal))
				{
					lock (_sync)
					{
						if (_pendingBestMove is not null) throw new InvalidOperationException("A client received concurrent searches.");
						_pendingBestMove = BestMoveFor(_position);
						_searches++;
						_activeSearches++;
						_maxActiveSearches = Math.Max(_maxActiveSearches, _activeSearches);
					}
					_started.TrySetResult();
					if (AutoComplete) CompletePending();
				}
				if (command == "stop")
				{
					_stopped.TrySetResult();
					if (CompleteOnStop) CompletePending();
				}
				return Task.CompletedTask;
			});
			Client = new UciEngineClient(transport, new()
			{
				ReadyTimeout = TimeSpan.FromSeconds(5),
				SearchStopGracePeriod = TimeSpan.FromSeconds(10)
			});
		}

		internal UciEngineClient Client { get; }
		internal IEnumerable<string> Commands => _commands.ToArray();
		internal Task SearchStarted => _started.Task;
		internal Task StopRequested => _stopped.Task;
		internal int SearchCount { get { lock (_sync) return _searches; } }
		internal int ActiveSearches { get { lock (_sync) return _activeSearches; } }
		internal int MaxActiveSearches { get { lock (_sync) return _maxActiveSearches; } }
		internal bool AutoComplete { get => _autoComplete; set => _autoComplete = value; }
		internal bool CompleteOnStop { get; init; } = true;

		internal void CompletePending(string evaluation = "score cp 0 wdl 400 200 400")
		{
			string? bestMove;
			lock (_sync)
			{
				bestMove = _pendingBestMove;
				if (bestMove is null) return;
				_pendingBestMove = null;
				_activeSearches--;
			}
			_channel.Writer.TryWrite($"info depth 1 {evaluation} pv {bestMove}");
			_channel.Writer.TryWrite($"bestmove {bestMove}");
		}

		private static string BestMoveFor(string position)
		{
			int marker = position.IndexOf(" moves ", StringComparison.Ordinal);
			if (marker < 0) return "d2d4";
			string[] moves = position[(marker + 7)..].Split(' ');
			return moves.Length switch
			{
				1 => moves[0] == "d2d4" ? "d7d5" : "c7c5",
				2 => "g1f3",
				3 => "b8c6",
				_ => throw new InvalidOperationException("Unexpected test history.")
			};
		}
	}

}
