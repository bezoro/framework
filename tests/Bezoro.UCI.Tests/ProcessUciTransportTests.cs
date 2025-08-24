using System.Diagnostics;
using Bezoro.UCI.Domain;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests;

[TestSubject(typeof(ProcessUciTransport))]
[Trait("Category", "Integration")]
public class ProcessUciTransportTests
{
	public const string STOCKFISH_PATH = "Engine/stockfish/stockfish-windows-x86-64-avx2.exe";

	[Fact]
	public async Task Dispose_ThenDisposeAsync_IsIdempotent()
	{
		var process = new ProcessUciTransport("any/nonempty/path");
		process.Dispose();

		await process.Awaiting(p => p.DisposeAsync().AsTask()).Should().NotThrowAsync();
	}

	[Fact]
	public async Task DisposeAsync_AfterStart_StopsProcessAndResetsIsStarted()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return; // No engine available -> skip test run without false failure.

		var process = new ProcessUciTransport(path);
		await process.StartAsync();

		await process.DisposeAsync();

		process.IsStarted.Should().BeFalse();
	}

	[Fact]
	public async Task DisposeAsync_BeforeStart_IsNoOpAndDoesNotThrow()
	{
		await using var process = new ProcessUciTransport("any/nonempty/path");
		await process.Awaiting(p => p.DisposeAsync().AsTask()).Should().NotThrowAsync();
		process.IsStarted.Should().BeFalse();
	}

	[Fact]
	public async Task DisposeAsync_SetsStatusDisposed()
	{
		await using var process = new ProcessUciTransport("any/nonempty/path");

		await process.DisposeAsync();

		process.Status.Should().Be(ProcessUciTransport.TransportStatus.Disposed);
	}

	[Fact]
	public async Task Events_HandlerExceptions_AreSwallowed_AndErrorRaised()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		await using var process = new ProcessUciTransport(path);

		var errorTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

		process.Exited += (_, _) => throw new InvalidOperationException("boom");
		process.Error  += ex => errorTcs.TrySetResult(ex);

		await process.StartAsync();
		await process.DisposeAsync();

		var completed = await Task.WhenAny(errorTcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
		completed.Should().Be(errorTcs.Task, "Error event should be raised when Exited handler throws");

		var ex = await errorTcs.Task;
		ex.Should().BeOfType<InvalidOperationException>();
	}

	[Fact]
	public async Task Exited_Event_IsRaised_OnProcessExit()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		var tcs = new TaskCompletionSource<(int? Code, string? Error)>(
			TaskCreationOptions.RunContinuationsAsynchronously);

		var process = new ProcessUciTransport(path);
		process.Exited += (code, error) => tcs.TrySetResult((code, error));

		await process.StartAsync();
		await process.DisposeAsync();

		var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
		completed.Should().Be(tcs.Task, "Exited event should be raised on process exit");

		var result = await tcs.Task;
		result.Code.HasValue.Should().BeTrue();
		result.Error.Should().BeNull();
	}

	[Fact]
	public async Task IsHealthy_AfterStopOrDispose_IsFalse()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		await using var process = new ProcessUciTransport(path);
		await process.StartAsync();

		await process.StopAsync();
		process.IsHealthy.Should().BeFalse();

		await process.StartAsync();
		await process.DisposeAsync();
		process.IsHealthy.Should().BeFalse();
	}

	[Fact]
	public async Task IsHealthy_WhenRunning_IsTrue()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		await using var process = new ProcessUciTransport(path);
		await process.StartAsync();

		process.IsHealthy.Should().BeTrue();

		await process.DisposeAsync();
	}

	[Fact]
	public async Task IsHealthy_WithoutRedirectedStderr_TrueWhileRunning()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		var             options = new ProcessUciTransportOptions { RedirectStandardError = false };
		await using var process = new ProcessUciTransport(path, null, null, options);

		await process.StartAsync();
		process.IsHealthy.Should().BeTrue();

		await process.DisposeAsync();
	}

	[Fact]
	public async Task ReadLinesAsync_AfterDispose_CompletesGracefully()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		await using var process = new ProcessUciTransport(path);
		await process.StartAsync();

		var enumerator = process.ReadLinesAsync(CancellationToken.None).GetAsyncEnumerator();
		var pending    = enumerator.MoveNextAsync().AsTask();

		await process.DisposeAsync();

		var completed = await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(5)));
		completed.Should().Be(pending, "enumeration should complete after dispose");
		(await pending).Should().BeFalse("no more lines should be available after transport is disposed");
	}

	[Fact]
	public async Task ReadLinesAsync_AfterStop_CompletesGracefully()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		await using var process = new ProcessUciTransport(path);
		await process.StartAsync();

		var enumerator = process.ReadLinesAsync(CancellationToken.None).GetAsyncEnumerator();
		var pending    = enumerator.MoveNextAsync().AsTask();

		await process.StopAsync();

		var completed = await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(5)));
		completed.Should().Be(pending, "enumeration should complete after stop");
		(await pending).Should().BeFalse("no more lines should be available after transport is stopped");

		await enumerator.DisposeAsync();
	}

	[Fact]
	public async Task ReadLinesAsync_WhenCalledWithoutEngineStart_ThrowsInvalidOperationException()
	{
		await using var process = new ProcessUciTransport("any/nonempty/path");

		var enumerator = process.ReadLinesAsync().GetAsyncEnumerator();

		await FluentActions
			  .Awaiting(async () => await enumerator.MoveNextAsync())
			  .Should()
			  .ThrowAsync<InvalidOperationException>();
	}

	[Fact]
	public async Task ReadLinesAsync_WithSecondConcurrentReader_ThrowsInvalidOperationException()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		await using var process = new ProcessUciTransport(path);
		await process.StartAsync();

		var e1        = process.ReadLinesAsync(CancellationToken.None).GetAsyncEnumerator();
		var firstMove = e1.MoveNextAsync().AsTask(); // occupy the single-reader slot

		var e2 = process.ReadLinesAsync(CancellationToken.None).GetAsyncEnumerator();
		await FluentActions.Awaiting(async () => await e2.MoveNextAsync())
						   .Should()
						   .ThrowAsync<InvalidOperationException>();

		await process.DisposeAsync();
	}

	[Fact]
	public async Task ReadLinesAsync_WithSingleReader_ReleasesGateAfterEnumeratorDisposed()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		await using var process = new ProcessUciTransport(path);
		await process.StartAsync();

		using var cts1 = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));
		var       e1   = process.ReadLinesAsync(cts1.Token).GetAsyncEnumerator();
		// Start enumeration to acquire the single-reader gate
		var firstMoveTask = e1.MoveNextAsync().AsTask();

		// Await the first move task: it will complete (likely due to cancellation), triggering finally to release the gate
		try
		{
			await firstMoveTask;
		}
		catch
		{
			// Cancellation or completion is fine
		}

		await e1.DisposeAsync();

		using var cts2 = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
		var       e2   = process.ReadLinesAsync(cts2.Token).GetAsyncEnumerator();

		await FluentActions.Awaiting(async () => await e2.MoveNextAsync())
						   .Should()
						   .NotThrowAsync<InvalidOperationException>();

		await e2.DisposeAsync();
		await process.DisposeAsync();
	}

	[Fact]
	public async Task ReadLinesAsync_WithSingleReaderFalse_AllowsConcurrentEnumerators()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		var             options = new ProcessUciTransportOptions { SingleReader = false };
		await using var process = new ProcessUciTransport(path, null, null, options);
		await process.StartAsync();

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
		var       e1  = process.ReadLinesAsync(cts.Token).GetAsyncEnumerator();
		var       e2  = process.ReadLinesAsync(cts.Token).GetAsyncEnumerator();

		// Should not fail with InvalidOperationException due to multiple readers
		await FluentActions.Awaiting(async () => await e1.MoveNextAsync())
						   .Should()
						   .NotThrowAsync<InvalidOperationException>();

		await FluentActions.Awaiting(async () => await e2.MoveNextAsync())
						   .Should()
						   .NotThrowAsync<InvalidOperationException>();

		await e1.DisposeAsync();
		await e2.DisposeAsync();
		await process.DisposeAsync();
	}

	[Fact]
	public async Task StartAsync_AfterDispose_ThrowsObjectDisposedException()
	{
		await using var process = new ProcessUciTransport("any/nonempty/path");
		await process.DisposeAsync();

		await FluentActions.Awaiting(() => process.StartAsync())
						   .Should()
						   .ThrowAsync<ObjectDisposedException>();
	}

	[Fact]
	public async Task StartAsync_ConcurrentCalls_StartsOnceAndBothComplete()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		await using var process = new ProcessUciTransport(path);

		var t1 = process.StartAsync();
		var t2 = process.StartAsync();

		await Task.WhenAll(t1, t2);

		process.IsStarted.Should().BeTrue();

		await process.DisposeAsync();
	}

	[Fact]
	public async Task StartAsync_WhenAlreadyStarted_IsIdempotent()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		await using var process = new ProcessUciTransport(path);
		await process.StartAsync();

		await process.StartAsync(); // second call should be a no-op and not throw

		process.IsStarted.Should().BeTrue();

		await process.DisposeAsync();
	}

	[Fact]
	public async Task StartAsync_WhenAlreadyStartedAndProcessAlive_ReturnsImmediately_NoStateChange()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		await using var process = new ProcessUciTransport(path);
		await process.StartAsync();

		var before = process.Status;
		await process.StartAsync(); // should be a fast no-op
		var after = process.Status;

		before.Should().Be(ProcessUciTransport.TransportStatus.Started);
		after.Should().Be(ProcessUciTransport.TransportStatus.Started);

		await process.DisposeAsync();
	}

	[Fact]
	public async Task StartAsync_WhenCalledWithInvalidProcess_ThrowsException()
	{
		await using var process = new ProcessUciTransport("invalid/path");

		await FluentActions.Awaiting(() => process.StartAsync()).Should().ThrowAsync<Exception>();
	}

	[Fact]
	public async Task StartAsync_WhenCalledWithValidProcess_StartsProcess()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return; // No engine available -> skip test run without false failure.

		await using var process = new ProcessUciTransport(path);

		await process.StartAsync();

		process.IsStarted.Should().BeTrue();
	}

	[Fact]
	public async Task StartAsync_WhenCanceled_ThrowsAndLeavesCleanState()
	{
		await using var process = new ProcessUciTransport("any/nonempty/path");
		using var       cts     = new CancellationTokenSource();
		cts.Cancel();

		await FluentActions.Awaiting(() => process.StartAsync(cts.Token))
						   .Should()
						   .ThrowAsync<OperationCanceledException>();

		process.IsStarted.Should().BeFalse();
	}

	[Fact]
	public async Task StartAsync_WhileStopping_ThrowsInvalidOperationException()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		await using var process = new ProcessUciTransport(path);
		await process.StartAsync();

		var stoppingTask = process.StopAsync(); // begin stopping but don't await

		// Wait until the status transitions to Stopping to avoid a race
		var sw = Stopwatch.StartNew();
		while (process.Status != ProcessUciTransport.TransportStatus.Stopping && sw.Elapsed < TimeSpan.FromSeconds(2))
			await Task.Delay(10);

		await FluentActions.Awaiting(() => process.StartAsync())
						   .Should()
						   .ThrowAsync<InvalidOperationException>();

		await stoppingTask; // clean up
	}

	[Fact]
	public async Task Status_Transitions_CreatedToStartedToStoppedToDisposed_AreObserved()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		await using var process = new ProcessUciTransport(path);

		process.Status.Should().Be(ProcessUciTransport.TransportStatus.Created);

		await process.StartAsync();
		process.Status.Should().Be(ProcessUciTransport.TransportStatus.Started);

		await process.StopAsync();
		process.Status.Should().Be(ProcessUciTransport.TransportStatus.Stopped);

		await process.DisposeAsync();
		process.Status.Should().Be(ProcessUciTransport.TransportStatus.Disposed);
	}

	[Fact]
	public async Task StopAsync_ConcurrentCalls_TeardownOnce_BothComplete()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		await using var process = new ProcessUciTransport(path);
		await process.StartAsync();

		var t1 = process.StopAsync();
		var t2 = process.StopAsync();

		await Task.WhenAll(t1, t2);

		process.Status.Should().Be(ProcessUciTransport.TransportStatus.Stopped);
	}

	[Fact]
	public async Task StopAsync_WhenCanceled_ThrowsOperationCanceledException_AndLeavesStateUnchanged()
	{
		await using var process = new ProcessUciTransport("any/nonempty/path");
		using var       cts     = new CancellationTokenSource();
		cts.Cancel();

		await FluentActions.Awaiting(() => process.StopAsync(cts.Token))
						   .Should()
						   .ThrowAsync<OperationCanceledException>();

		process.Status.Should().Be(ProcessUciTransport.TransportStatus.Created);
	}

	[Fact]
	public async Task StopAsync_WhenNotStarted_NoOpAndSetsStatusStopped()
	{
		await using var process = new ProcessUciTransport("any/nonempty/path");

		await process.StopAsync();

		process.Status.Should().Be(ProcessUciTransport.TransportStatus.Stopped);
	}

	[Fact]
	public async Task TryWriteLineAsync_WhenCalledWithoutEngineStart_ThrowsInvalidOperationException()
	{
		await using var process = new ProcessUciTransport("any/nonempty/path");

		await FluentActions.Awaiting(() => process.TryWriteLineAsync("uci", TimeSpan.FromMilliseconds(10)))
						   .Should()
						   .ThrowAsync<InvalidOperationException>();
	}

	[Fact]
	public async Task TryWriteLineAsync_WhenChannelReady_ReturnsTrue()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		await using var process = new ProcessUciTransport(path);
		await process.StartAsync();

		bool ok = await process.TryWriteLineAsync("uci", TimeSpan.FromSeconds(1));
		ok.Should().BeTrue();

		await process.DisposeAsync();
	}

	[Fact]
	public async Task TryWriteLineAsync_WithNegativeNonInfiniteTimeout_ThrowsArgumentOutOfRangeException()
	{
		await using var process = new ProcessUciTransport("any/nonempty/path");

		await FluentActions.Awaiting(() => process.TryWriteLineAsync("uci", TimeSpan.FromMilliseconds(-2)))
						   .Should()
						   .ThrowAsync<ArgumentOutOfRangeException>();
	}

	[Fact]
	public async Task WriteLineAsync_AfterDispose_ThrowsInvalidOperationException()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		await using var process = new ProcessUciTransport(path);
		await process.StartAsync();
		await process.DisposeAsync();

		await FluentActions.Awaiting(() => process.WriteLineAsync("uci"))
						   .Should()
						   .ThrowAsync<InvalidOperationException>();
	}

	[Fact]
	public async Task WriteLineAsync_AfterStop_ThrowsInvalidOperationException()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		await using var process = new ProcessUciTransport(path);
		await process.StartAsync();
		await process.StopAsync();

		await FluentActions.Awaiting(() => process.WriteLineAsync("uci"))
						   .Should()
						   .ThrowAsync<InvalidOperationException>();
	}

	[Fact]
	public async Task WriteLineAsync_WhenCalledWithCarriageReturn_ThrowsArgumentException()
	{
		await using var process = new ProcessUciTransport("any/nonempty/path");

		await FluentActions.Awaiting(() => process.WriteLineAsync("uci\r"))
						   .Should()
						   .ThrowAsync<ArgumentException>();
	}

	[Fact]
	public async Task WriteLineAsync_WhenCalledWithEngineStart_WritesLineToEngine()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return; // No engine available -> skip test run without false failure.

		await using var process = new ProcessUciTransport(path);
		await process.StartAsync();

		await process.WriteLineAsync("uci");

		string?   output = null;
		using var cts    = new CancellationTokenSource(TimeSpan.FromSeconds(5));

		await foreach (string line in process.ReadLinesAsync(cts.Token))
		{
			if (!string.Equals(line.Trim(), "uciok", StringComparison.OrdinalIgnoreCase)) continue;

			output = line;
			break;
		}

		output.Should().Be("uciok", "the engine should acknowledge UCI initialization with 'uciok' within timeout");
	}

	[Fact]
	public async Task WriteLineAsync_WhenCalledWithNewline_ThrowsArgumentException()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		await using var process = new ProcessUciTransport(path);
		await process.StartAsync();

		await FluentActions
			  .Awaiting(() => process.WriteLineAsync("uci\nisready"))
			  .Should()
			  .ThrowAsync<ArgumentException>();
	}

	[Fact]
	public async Task WriteLineAsync_WhenCalledWithNull_ThrowsArgumentNullException()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		await using var process = new ProcessUciTransport(path);
		await process.StartAsync();

		// ReSharper disable once AssignNullToNotNullAttribute
		await FluentActions
			  .Awaiting(() => process.WriteLineAsync(null!))
			  .Should()
			  .ThrowAsync<ArgumentNullException>();
	}

	[Fact]
	public async Task WriteLineAsync_WhenCalledWithoutEngineStart_ThrowsException()
	{
		await using var process = new ProcessUciTransport("any/nonempty/path");

		await FluentActions
			  .Awaiting(() => process.WriteLineAsync("uci"))
			  .Should()
			  .ThrowAsync<InvalidOperationException>();
	}

	[Fact]
	public async Task WriteLineAsync_WhenCalledWithWhitespace_ThrowsArgumentException()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		await using var process = new ProcessUciTransport(path);
		await process.StartAsync();

		await FluentActions
			  .Awaiting(() => process.WriteLineAsync("   "))
			  .Should()
			  .ThrowAsync<ArgumentException>();
	}

	[Fact]
	public async Task WriteLineAsync_WhenCanceled_ThrowsOperationCanceledException()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		await using var process = new ProcessUciTransport(path);
		await process.StartAsync();

		using var cts = new CancellationTokenSource();
		cts.Cancel();

		await FluentActions.Awaiting(() => process.WriteLineAsync("uci", cts.Token))
						   .Should()
						   .ThrowAsync<OperationCanceledException>();

		await process.DisposeAsync();
	}

	[Fact]
	public async Task WriteLineAsync_WithUnknownCommand_DoesNotThrowAndReadingContinues()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return;

		await using var process = new ProcessUciTransport(path);
		await process.StartAsync();

		await FluentActions.Awaiting(() => process.WriteLineAsync("this_is_not_a_real_command"))
						   .Should()
						   .NotThrowAsync();

		await process.WriteLineAsync("uci");

		string?   output = null;
		using var cts    = new CancellationTokenSource(TimeSpan.FromSeconds(5));

		await foreach (string line in process.ReadLinesAsync(cts.Token))
		{
			if (!string.Equals(line.Trim(), "uciok", StringComparison.OrdinalIgnoreCase)) continue;

			output = line;
			break;
		}

		output.Should().Be("uciok", "reading should continue after unknown command");
	}

	[Fact]
	public void Constructor_Default_StatusIsCreated()
	{
		var process = new ProcessUciTransport("any/nonempty/path");
		process.Status.Should().Be(ProcessUciTransport.TransportStatus.Created);
	}

	[Fact]
	public void Constructor_WithNullPath_ThrowsArgumentException()
	{
		// ReSharper disable once AssignNullToNotNullAttribute
		Action act = () => _ = new ProcessUciTransport(null!);
		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void Constructor_WithWhitespacePath_ThrowsArgumentException()
	{
		Action act = () => _ = new ProcessUciTransport("   ");
		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void Options_ChannelCapacity_LessOrEqualZero_Throws()
	{
		var    options = new ProcessUciTransportOptions { ChannelCapacity = 0 };
		Action act     = () => _ = new ProcessUciTransport("any/nonempty/path", null, null, options);

		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void Options_NewLine_Empty_Throws()
	{
		var    options = new ProcessUciTransportOptions { NewLine = "" };
		Action act     = () => _ = new ProcessUciTransport("any/nonempty/path", null, null, options);

		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void Options_QuitGracePeriod_Negative_Throws()
	{
		var    options = new ProcessUciTransportOptions { QuitGracePeriod = TimeSpan.FromMilliseconds(-1) };
		Action act     = () => _ = new ProcessUciTransport("any/nonempty/path", null, null, options);

		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void Options_QuitGracePeriodDefault_AndQuitGracePeriodMsNegative_Throws()
	{
		var    options = new ProcessUciTransportOptions { QuitGracePeriod = TimeSpan.Zero, QuitGracePeriodMs = -1 };
		Action act     = () => _ = new ProcessUciTransport("any/nonempty/path", null, null, options);

		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	private static string? TryResolveEnginePath()
	{
		string? fromEnv = Environment.GetEnvironmentVariable("STOCKFISH_PATH");
		if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv))
			return fromEnv;

		return File.Exists(STOCKFISH_PATH) ? STOCKFISH_PATH : null;
	}
}
