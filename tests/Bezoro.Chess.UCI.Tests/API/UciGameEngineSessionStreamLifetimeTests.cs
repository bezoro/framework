using System.Reflection;
using Bezoro.Chess.UCI.API;

namespace Bezoro.Chess.UCI.Tests.API;

public sealed class UciGameEngineSessionStreamLifetimeTests
{
	[Fact]
	public async Task StateStream_WhenConsumerFallsBehind_YieldsOnlyTheLatestPendingSnapshot()
	{
		await using var session = new UciGameEngineSession();
		using var cancellation = new CancellationTokenSource();
		await using var stream = session.StreamStateAsync(cancellation.Token).GetAsyncEnumerator();
		Assert.True(await stream.MoveNextAsync());
		Assert.Equal(Fen.Default, stream.Current.CurrentFen);

		// The reader remains paused while actual session operations publish a backlog.
		for (int i = 0; i < 100; i++)
		{
			await session.OfferDrawAsync();
			await session.DeclineDrawAsync();
		}
		await session.MakeMoveAsync("e2e4");

		Assert.True(await stream.MoveNextAsync());
		Assert.Equal(session.State, stream.Current);
		Assert.Equal("e2e4", Assert.Single(stream.Current.PlayedMoves));

		var pendingRead = stream.MoveNextAsync().AsTask();
		Assert.False(pendingRead.IsCompleted);
		cancellation.Cancel();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pendingRead);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task StreamSubscription_WhenDisposalWinsTheRegistrationRace_RejectsTheReader(bool classifiedMoves)
	{
		await using var session = new UciGameEngineSession();
		using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		await using var states = session.StreamStateAsync(cancellation.Token).GetAsyncEnumerator();
		await using var moves = session.StreamClassifiedMovesAsync(cancellation.Token).GetAsyncEnumerator();
		Func<ValueTask<bool>> moveNext = classifiedMoves ? moves.MoveNextAsync : states.MoveNextAsync;
		var sync = typeof(UciGameEngineSession).GetField("_sync", BindingFlags.Instance | BindingFlags.NonPublic)!
			.GetValue(session)!;
		using var entering = new ManualResetEventSlim();
		Thread? readerThread = null;
		Task<bool> read;
		ValueTask disposal;

		Monitor.Enter(sync);
		try
		{
			read = Task.Run(() =>
			{
				readerThread = Thread.CurrentThread;
				entering.Set();
				return moveNext().AsTask();
			});
			Assert.True(entering.Wait(TimeSpan.FromSeconds(5)));
			// The only blocking call on this worker is the session's subscription lock.
			// This puts disposal after the early flag check and before registration, with no sleeps.
			Assert.True(SpinWait.SpinUntil(
				() => (readerThread!.ThreadState & ThreadState.WaitSleepJoin) != 0,
				TimeSpan.FromSeconds(5)));
			disposal = session.DisposeAsync();
			Assert.True(disposal.IsCompletedSuccessfully);
		}
		finally { Monitor.Exit(sync); }

		await disposal;
		await Assert.ThrowsAsync<ObjectDisposedException>(() => read.WaitAsync(TimeSpan.FromSeconds(5)));
	}

	[Fact]
	public async Task LocalSession_MetadataAndLifecycle_DoNotRequireAnEngineRuntime()
	{
		await using var session = new UciGameEngineSession();
		Assert.False(session.IsStarted);
		Assert.False(session.IsHealthy);
		Assert.Equal(UciEngineInfo.Empty, session.EngineInfo);
		Assert.Empty(session.AvailableOptions);
		Assert.Equal(UciEngineCapabilities.Unknown, session.Capabilities);
		await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartAsync());
		await Assert.ThrowsAsync<InvalidOperationException>(() => session.ClassifyMoveAsync("e2e4"));
		await Assert.ThrowsAsync<InvalidOperationException>(() => session.SearchAsync(new() { Depth = 1 }));

		await session.MakeMoveAsync("e2e4");
		await session.StopSearchAsync();
		await session.StopAsync();
		Assert.False(session.IsStarted);
	}
}
