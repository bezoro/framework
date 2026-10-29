using System.Collections.Concurrent;
using Bezoro.Chess.UCI.Protocol.Domain.EngineClient;
using NSubstitute;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain;

public class UciSearchCoordinatorCleanupTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task StopSearch_WhenBestMoveArrivedDuringCleanup_ShouldWaitUntilSearchSlotIsReleased(bool cancelStop)
	{
		var transport = Substitute.For<IUciTransport>();
		transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		using var releaseCleanup = new ManualResetEventSlim();
		using var cancellation = new CancellationTokenSource();
		var cleanupEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		Task? stopped = null;
		var idleNotifications = 0;
		UciSearchCoordinator? search = null;
		search = new(transport, activity =>
		{
			if (activity != EngineActivity.Idle || Interlocked.Increment(ref idleNotifications) != 2) return;
			// bestmove is already available, but the original GoAsync still owns the search slot.
			stopped = search!.StopSearchAsync(cancellation.Token);
			if (cancelStop) cancellation.Cancel();
			cleanupEntered.TrySetResult();
			if (!releaseCleanup.Wait(TimeSpan.FromSeconds(5)))
				throw new TimeoutException("The test did not release search cleanup.");
		}, new());

		var firstSearch = search.ExecuteSearchAsync(new() { Infinite = true }, default);
		try
		{
			search.HandleBestMove(new("e2e4", "", "bestmove e2e4"));
			await cleanupEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
			Assert.NotNull(stopped);
			Assert.False(stopped.IsCompleted);
		}
		finally
		{
			releaseCleanup.Set();
		}

		if (cancelStop)
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stopped!.WaitAsync(TimeSpan.FromSeconds(5)));
		else
			await stopped!.WaitAsync(TimeSpan.FromSeconds(5));
		await search.ExecuteFireAndForgetAsync(new() { Infinite = true }, default);
		Assert.False(firstSearch.IsFaulted);
		Assert.Equal("e2e4", (await firstSearch).BestMove);
		search.HandleBestMove(new("d2d4", "", "bestmove d2d4"));
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task StopSearch_WhenCanceledOrTimedOutSearchStillWritesStop_ShouldWaitBeforeAllowingNextSearch(bool cancelSearch)
	{
		var transport = Substitute.For<IUciTransport>();
		var commands = new ConcurrentQueue<string>();
		var searchStopEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var releaseSearchStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var stopCount = 0;
		transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
		{
			string command = call.ArgAt<string>(0);
			commands.Enqueue(command);
			if (command != "stop" || Interlocked.Increment(ref stopCount) != 1) return Task.CompletedTask;
			searchStopEntered.TrySetResult();
			return releaseSearchStop.Task;
		});
		EngineActivity activity = EngineActivity.Idle;
		var search = new UciSearchCoordinator(transport, value => activity = value, new()
		{
			InfiniteSearchTimeout = cancelSearch ? Timeout.InfiniteTimeSpan : TimeSpan.Zero
		});
		using var cancellation = new CancellationTokenSource();
		var firstSearch = search.ExecuteSearchAsync(new() { Infinite = true }, cancellation.Token);
		if (cancelSearch) cancellation.Cancel();
		await searchStopEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

		Task stopped;
		try
		{
			search.HandleBestMove(new("e2e4", "", "bestmove e2e4"));
			stopped = search.StopSearchAsync(default);
			Assert.False(stopped.IsCompleted);
		}
		finally
		{
			releaseSearchStop.TrySetResult();
		}

		await stopped.WaitAsync(TimeSpan.FromSeconds(5));
		await search.ExecuteFireAndForgetAsync(new() { Infinite = true }, default);
		if (cancelSearch)
			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => firstSearch);
		else
			Assert.Equal("e2e4", (await firstSearch).BestMove);
		Assert.Equal(EngineActivity.Searching, activity);
		Assert.Equal(new[] { "go infinite", "stop", "stop", "go infinite" }, commands.ToArray());
		search.HandleBestMove(new("d2d4", "", "bestmove d2d4"));
	}
}
