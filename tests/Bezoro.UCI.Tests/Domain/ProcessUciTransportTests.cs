using System.Diagnostics;
using System.Reflection;
using System.Threading.Channels;
using Bezoro.UCI.Domain;
using Bezoro.UCI.Tests._Resources;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Domain;

[TestSubject(typeof(ProcessUciTransport))]
public static class ProcessUciTransportTests
{
	private static string? TryResolveCmdPath()
	{
		string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
		if (!string.IsNullOrWhiteSpace(systemRoot))
		{
			string cmd = Path.Combine(systemRoot, "System32", "cmd.exe");
			if (File.Exists(cmd)) return cmd;
		}

		string envCmd = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\cmd.exe");
		return File.Exists(envCmd) ? envCmd : null;
	}

	public class Integration
	{
		[Fact]
		public async Task DisposeAsync_AfterStart_StopsProcessAndResetsIsStarted()
		{
			const string path    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(path);
			await process.StartAsync();

			await process.DisposeAsync();

			process.IsStarted.Should().BeFalse();
		}

		[Fact]
		public async Task DisposeAsync_MultipleCalls_ShouldBeIdempotent()
		{
			const string path    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(path);
			await process.StartAsync();

			await process.DisposeAsync();
			await process.DisposeAsync();

			process.IsStarted.Should().BeFalse();
			process.Status.Should().Be(ProcessUciTransport.TransportStatus.Disposed);
		}

		[Fact]
		public async Task Exited_Event_IsRaised_OnProcessExit()
		{
			const string path = TestConsts.STOCKFISH_PATH;
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
		public async Task Exited_Event_IsRaisedOnlyOnce()
		{
			var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH);

			var count = 0;
			var tcs   = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

			transport.Exited += (_, _) =>
			{
				Interlocked.Increment(ref count);
				tcs.TrySetResult(null);
			};

			await transport.StartAsync();
			await transport.DisposeAsync();

			var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
			completed.Should().Be(tcs.Task);

			// Give a brief moment to ensure no duplicate invocations occur
			await Task.Delay(100);

			count.Should().Be(1);
		}

		[Fact]
		public async Task Exited_HandlerThrows_ErrorEventIsRaisedAndNoCrash()
		{
			// Use a quickly exiting process
			var transport = new ProcessUciTransport(
				TestConsts.STOCKFISH_PATH,
				[ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, ProcessArgs.ZERO]);

			var errorTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
			transport.Error += ex => errorTcs.TrySetResult(ex);

			// Exited handler will throw; transport must swallow and surface via Error event
			transport.Exited += (_, _) => throw new InvalidOperationException("boom from handler");

			await transport.StartAsync();

			var completed = await Task.WhenAny(errorTcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
			completed.Should().Be(errorTcs.Task, "Error event should be raised when Exited handler throws");

			(await errorTcs.Task).Should().BeOfType<InvalidOperationException>();

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task IsHealthy_AfterStopOrDispose_IsFalse()
		{
			const string path    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(path);
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
			const string path    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(path);
			await process.StartAsync();

			process.IsHealthy.Should().BeTrue();

			await process.DisposeAsync();
		}

		[Fact]
		public async Task ReadLinesAsync_AfterDispose_CompletesGracefully()
		{
			const string? path    = TestConsts.STOCKFISH_PATH;
			var           process = new ProcessUciTransport(path);
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
			const string    path    = TestConsts.STOCKFISH_PATH;
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
		public async Task ReadLinesAsync_WithSecondConcurrentReader_ThrowsInvalidOperationException()
		{
			const string path    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(path);
			await process.StartAsync();

			var e1 = process.ReadLinesAsync(CancellationToken.None).GetAsyncEnumerator();
			await e1.MoveNextAsync().AsTask();

			var e2 = process.ReadLinesAsync(CancellationToken.None).GetAsyncEnumerator();
			await FluentActions.Awaiting(async () => await e2.MoveNextAsync())
							   .Should()
							   .ThrowAsync<InvalidOperationException>();

			await process.DisposeAsync();
		}

		[Fact]
		public async Task ReadLinesAsync_WithSingleReader_ReleasesGateAfterEnumeratorDisposed()
		{
			const string path    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(path);
			await process.StartAsync();

			using var cts1 = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));
			var       e1   = process.ReadLinesAsync(cts1.Token).GetAsyncEnumerator(cts1.Token);
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
			var       e2   = process.ReadLinesAsync(cts2.Token).GetAsyncEnumerator(cts2.Token);

			await FluentActions.Awaiting(async () => await e2.MoveNextAsync())
							   .Should()
							   .NotThrowAsync<InvalidOperationException>();

			await e2.DisposeAsync();
			await process.DisposeAsync();
		}

		[Fact]
		public async Task ReadLinesAsync_WithSingleReaderFalse_AllowsConcurrentEnumerators()
		{
			const string path    = TestConsts.STOCKFISH_PATH;
			var          options = new ProcessUciTransportOptions { SingleReader = false };
			var          process = new ProcessUciTransport(path, null, null, options);
			await process.StartAsync();

			using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
			var       e1  = process.ReadLinesAsync(cts.Token).GetAsyncEnumerator(cts.Token);
			var       e2  = process.ReadLinesAsync(cts.Token).GetAsyncEnumerator(cts.Token);

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
		public async Task ReadLoop_Backpressure_ChannelFull_IncrementsBackpressureEvents()
		{
			// Resolve cmd only for this test; skip if unavailable (e.g., non-Windows environment)
			string? cmdPath = TryResolveCmdPath();
			if (cmdPath is null) return;

			var options = new ProcessUciTransportOptions
			{
				ChannelCapacity = 1 // ensure backpressure when more than one line is produced
			};

			// Produce two non-empty lines quickly so the read loop's writer.TryWrite fails on the second
			var transport = new ProcessUciTransport(
				cmdPath,
				[ProcessArgs.CMD_EXECUTE, ProcessArgs.ECHO, "L1", ProcessArgs.CHAIN, ProcessArgs.ECHO, "L2"],
				null,
				options);

			await transport.StartAsync();

			// Do not consume any lines; allow read loop to hit channel backpressure path
			await Task.Delay(200);

			await transport.StopAsync();

			transport.BackpressureEvents.Should().BeGreaterThan(0);

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task ReadLoop_SkipsEmptyLines_OnlyNonEmptyReceived()
		{
			string? cmdPath = TryResolveCmdPath();
			if (cmdPath is null) return;

			var transport = new ProcessUciTransport(
				cmdPath,
				[ProcessArgs.CMD_EXECUTE, ProcessArgs.ECHO_EMPTY, ProcessArgs.CHAIN, ProcessArgs.ECHO, "marker"]);

			await transport.StartAsync();

			string?   received = null;
			using var cts      = new CancellationTokenSource(TimeSpan.FromSeconds(5));
			await foreach (string line in transport.ReadLinesAsync(cts.Token))
			{
				if (string.IsNullOrWhiteSpace(line)) continue;

				received = line;
				break;
			}

			received.Should().NotBeNull();
			received!.Trim().Should().Be("marker");

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task StartAsync_ConcurrentCalls_StartsOnceAndBothComplete()
		{
			const string path    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(path);

			var t1 = process.StartAsync();
			var t2 = process.StartAsync();

			await Task.WhenAll(t1, t2);

			process.IsStarted.Should().BeTrue();

			await process.DisposeAsync();
		}

		[Fact]
		public async Task StartAsync_WhenAlreadyStarted_IsIdempotent()
		{
			const string path    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(path);
			await process.StartAsync();

			var before = process.Status;

			await process.StartAsync(); // second call should be a no-op and not throw
			var after = process.Status;

			process.IsStarted.Should().BeTrue();
			before.Should().Be(ProcessUciTransport.TransportStatus.Started);
			after.Should().Be(ProcessUciTransport.TransportStatus.Started);

			await process.DisposeAsync();
		}


		[Fact]
		public async Task StartAsync_WhenCalledWithValidProcess_StartsProcess()
		{
			const string    path    = TestConsts.STOCKFISH_PATH;
			await using var process = new ProcessUciTransport(path);

			await process.StartAsync();

			process.IsStarted.Should().BeTrue();
		}

		[Fact]
		public async Task StartAsync_WhenConcurrent_ShouldStartOnceAndAwaitOthers()
		{
			const string path    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(path);

			var t1 = process.StartAsync();
			var t2 = process.StartAsync();
			var t3 = process.StartAsync();

			await Task.WhenAll(t1, t2, t3);

			process.IsStarted.Should().BeTrue();
			process.Status.Should().Be(ProcessUciTransport.TransportStatus.Started);

			await process.DisposeAsync();
		}

		[Fact]
		public async Task StartAsync_WhileStopping_ThrowsInvalidOperationException()
		{
			const string path    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(path);
			await process.StartAsync();

			var stoppingTask = process.StopAsync(); // begin stopping but don't await

			// Wait until the status transitions to Stopping to avoid a race
			var sw = Stopwatch.StartNew();
			while (process.Status != ProcessUciTransport.TransportStatus.Stopping &&
				   sw.Elapsed < TimeSpan.FromSeconds(2))
				await Task.Delay(10);

			await FluentActions.Awaiting(() => process.StartAsync())
							   .Should()
							   .ThrowAsync<InvalidOperationException>();

			await stoppingTask; // clean up
			await process.DisposeAsync();
		}

		[Fact]
		public async Task StartAsync_WithInvalidWorkingDirectory_ThrowsArgumentException()
		{
			const string path       = TestConsts.STOCKFISH_PATH;
			string       invalidDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
			var          process    = new ProcessUciTransport(path, null, invalidDir);

			await FluentActions.Awaiting(() => process.StartAsync())
							   .Should()
							   .ThrowAsync<ArgumentException>();

			await process.DisposeAsync();
		}

		[Fact]
		public async Task StartAsync_WithPreexistingExitedProcessObject_CleansUpAndStarts()
		{
			string? cmdPath = TryResolveCmdPath();
			if (cmdPath is null) return;

			// Create a process that exits immediately
			var psi = new ProcessStartInfo
			{
				FileName        = cmdPath,
				UseShellExecute = false
			};

			psi.ArgumentList.Add("/c");
			psi.ArgumentList.Add("exit");
			psi.ArgumentList.Add("0");

			using var exitedProcess = new Process();
			exitedProcess.StartInfo           = psi;
			exitedProcess.EnableRaisingEvents = true;
			exitedProcess.Start();
			exitedProcess.WaitForExit();

			// Assign the exited process to transport._process before StartAsync to hit cleanup path
			var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH);
			var processField = typeof(ProcessUciTransport).GetField(
				"_process",
				BindingFlags.Instance | BindingFlags.NonPublic);

			processField.Should().NotBeNull();
			processField!.SetValue(transport, exitedProcess);

			await transport.StartAsync();

			transport.IsStarted.Should().BeTrue();

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task Status_Transitions_CreatedToStartedToStoppedToDisposed_AreObserved()
		{
			const string path    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(path);

			process.Status.Should().Be(ProcessUciTransport.TransportStatus.Created);

			await process.StartAsync();
			process.Status.Should().Be(ProcessUciTransport.TransportStatus.Started);

			await process.StopAsync();
			process.Status.Should().Be(ProcessUciTransport.TransportStatus.Stopped);

			await process.DisposeAsync();
			process.Status.Should().Be(ProcessUciTransport.TransportStatus.Disposed);
		}

		[Fact]
		public async Task StderrDisabled_ShouldNotStartStderrLoop()
		{
			string? cmdPath = TryResolveCmdPath();
			if (cmdPath is null) return;

			var options = new ProcessUciTransportOptions
			{
				RedirectStandardError = false
			};

			var transport = new ProcessUciTransport(
				cmdPath,
				[ProcessArgs.CMD_EXECUTE, ProcessArgs.ECHO, "HelloErr", ProcessArgs.STD_OUT_TO_STD_ERR],
				null,
				options);

			var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
			transport.StderrReceived += s =>
			{
				if (!string.IsNullOrEmpty(s)) tcs.TrySetResult(s);
			};

			await transport.StartAsync();

			// Since stderr isn't redirected, the event should not fire within a short window
			var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromMilliseconds(500)));
			completed.Should().NotBe(tcs.Task);

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task StderrReceived_Event_Fires_WhenRedirected()
		{
			string? cmdPath = TryResolveCmdPath();
			if (cmdPath is null) return;

			var options = new ProcessUciTransportOptions { RedirectStandardError = true };
			var transport = new ProcessUciTransport(
				cmdPath,
				[
					ProcessArgs.CMD_EXECUTE, ProcessArgs.ECHO, "HelloStderr", ProcessArgs.STD_OUT_TO_STD_ERR
				],
				null,
				options);

			var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
			transport.StderrReceived += s =>
			{
				if (s.Contains("HelloStderr")) tcs.TrySetResult(s);
			};

			await transport.StartAsync();

			var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
			completed.Should().Be(tcs.Task);

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task StderrReceived_HandlerThrows_IsSwallowed_NoCrash()
		{
			string? cmdPath = TryResolveCmdPath();
			if (cmdPath is null) return;

			var options = new ProcessUciTransportOptions { RedirectStandardError = true };
			var transport = new ProcessUciTransport(
				cmdPath,
				new[] { ProcessArgs.CMD_EXECUTE, ProcessArgs.ECHO, "oops", ProcessArgs.STD_OUT_TO_STD_ERR },
				null,
				options);

			var errorTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
			transport.Error += ex => errorTcs.TrySetResult(ex);

			// Handler intentionally throws; transport should swallow and NOT surface via Error.
			transport.StderrReceived += _ => throw new InvalidOperationException("stderr handler boom");

			await transport.StartAsync();

			// Allow stderr loop to process and observe whether Error was raised due to the thrown handler.
			var completed = await Task.WhenAny(errorTcs.Task, Task.Delay(TimeSpan.FromMilliseconds(500)));

			// If handler exceptions were not swallowed, Error would fire.
			completed.Should().NotBe(
				errorTcs.Task,
				"stderr handler exceptions must be swallowed and not surface via Error");

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task StopAsync_ConcurrentCall_WithCanceledToken_CancelsWhileAwaitingExistingStop()
		{
			var options = new ProcessUciTransportOptions
			{
				TeardownTimeout = TimeSpan.FromSeconds(1) // make first StopAsync take a bit
			};

			var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH, null, null, options);
			await transport.StartAsync();

			// Replace _exitNotifyTask with a never-completing task to ensure Stop waits for TryAwaitWithTimeout path
			var tcsNever = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
			var field = typeof(ProcessUciTransport).GetField(
				"_exitNotifyTask",
				BindingFlags.Instance | BindingFlags.NonPublic);

			field.Should().NotBeNull();
			field!.SetValue(transport, tcsNever.Task);

			// Start first stop (do not await)
			var firstStop = transport.StopAsync();

			// Wait until stopping TCS is published to avoid a race
			var stoppingField = typeof(ProcessUciTransport).GetField(
				"_stoppingTcs",
				BindingFlags.Instance | BindingFlags.NonPublic);

			var sw = Stopwatch.StartNew();
			while (stoppingField!.GetValue(transport) is null && sw.Elapsed < TimeSpan.FromSeconds(2))
				await Task.Delay(10);

			// Second stop with cancellation: should cancel while awaiting existing stop
			var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
			await FluentActions.Awaiting(() => transport.StopAsync(cts.Token))
							   .Should()
							   .ThrowAsync<OperationCanceledException>();

			// Ensure the first completes and state is consistent
			await firstStop;
			transport.Status.Should().Be(ProcessUciTransport.TransportStatus.Stopped);

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task StopAsync_ShouldDisposeStreamsAndCompleteChannels()
		{
			const string path    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(path);
			await process.StartAsync();

			var enumerator = process.ReadLinesAsync(CancellationToken.None).GetAsyncEnumerator();
			var pending    = enumerator.MoveNextAsync().AsTask();

			await process.StopAsync();

			var completed = await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(5)));
			completed.Should().Be(pending);
			(await pending).Should().BeFalse();

			await enumerator.DisposeAsync();

			await process.DisposeAsync();
		}

		[Fact]
		public async Task StopAsync_ShouldSendQuitThenKillAfterGrace()
		{
			string? cmdPath = TryResolveCmdPath();
			if (cmdPath is null) return;

			var options = new ProcessUciTransportOptions
			{
				QuitGracePeriod = TimeSpan.FromMilliseconds(100)
			};

			var transport = new ProcessUciTransport(cmdPath, [ProcessArgs.CMD_KEEP], null, options);
			await transport.StartAsync();

			var sw = Stopwatch.StartNew();
			await transport.StopAsync();
			sw.Stop();

			sw.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(50));
			transport.Status.Should().Be(ProcessUciTransport.TransportStatus.Stopped);

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task TryWriteLineAsync_AfterDispose_ThrowsInvalidOperationException()
		{
			const string path = TestConsts.STOCKFISH_PATH;

			var process = new ProcessUciTransport(path);
			await process.StartAsync();
			await process.DisposeAsync();

			await FluentActions.Awaiting(() => process.TryWriteLineAsync("uci", TimeSpan.FromMilliseconds(10)))
							   .Should()
							   .ThrowAsync<InvalidOperationException>();
		}

		[Fact]
		public async Task TryWriteLineAsync_AfterStop_ThrowsInvalidOperationException()
		{
			const string path    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(path);
			await process.StartAsync();
			await process.StopAsync();

			await FluentActions.Awaiting(() => process.TryWriteLineAsync("uci", TimeSpan.FromMilliseconds(10)))
							   .Should()
							   .ThrowAsync<InvalidOperationException>();
		}

		[Fact]
		public async Task TryWriteLineAsync_ChannelClosedDuringStop_ThrowsInvalidOperationException()
		{
			var options = new ProcessUciTransportOptions
			{
				ChannelCapacity  = 1,
				DisableWriteLoop = true
			};

			var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH, null, null, options);

			await transport.StartAsync();

			// Fill channel to force slow write path
			bool ok1 = await transport.TryWriteLineAsync("uci", TimeSpan.FromMilliseconds(10));
			ok1.Should().BeTrue();

			var writeTask = transport.TryWriteLineAsync("isready", TimeSpan.FromSeconds(5));

			// Immediately stop transport which should close the channel causing ChannelClosedException -> InvalidOperationException
			await transport.Awaiting(_ => transport.StopAsync()).Should().NotThrowAsync();

			await FluentActions.Awaiting(async () => await writeTask)
							   .Should()
							   .ThrowAsync<InvalidOperationException>()
							   .WithMessage("Transport is stopping or stopped; cannot write.");
		}

		[Fact]
		public async Task TryWriteLineAsync_ChannelFull_TinyTimeout_SpinPath_ReturnsFalse()
		{
			var options = new ProcessUciTransportOptions
			{
				ChannelCapacity            = 1,
				DisableWriteLoop           = true,
				SmallTimeoutSpinIterations = 10
			};

			var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH, null, null, options);

			await transport.StartAsync();

			bool ok1 = await transport.TryWriteLineAsync("uci", TimeSpan.FromMilliseconds(10));
			ok1.Should().BeTrue();

			bool ok2 = await transport.TryWriteLineAsync("isready", TimeSpan.FromMilliseconds(1));
			ok2.Should().BeFalse();

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task TryWriteLineAsync_ChannelFull_ZeroTimeout_ReturnsFalse()
		{
			var options = new ProcessUciTransportOptions
			{
				ChannelCapacity  = 1,
				DisableWriteLoop = true, // test-only hook to keep channel full
				ValidateCommands = true
			};

			var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH, null, null, options);
			await transport.StartAsync();

			// Fill the channel
			bool ok1 = await transport.TryWriteLineAsync("uci", TimeSpan.FromMilliseconds(10));
			ok1.Should().BeTrue();

			bool ok2 = await transport.TryWriteLineAsync("isready", TimeSpan.Zero);
			ok2.Should().BeFalse();

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task TryWriteLineAsync_InfiniteTimeout_PathCompletes()
		{
			var options = new ProcessUciTransportOptions
			{
				ChannelCapacity  = 1,
				DisableWriteLoop = false
			};

			var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH, null, null, options);

			await transport.StartAsync();

			// Attempt to create contention: first write fills channel, second goes to slow path with infinite timeout.
			bool ok1 = await transport.TryWriteLineAsync("uci", TimeSpan.FromMilliseconds(10));
			ok1.Should().BeTrue();

			// This may briefly block until write loop drains; should return true.
			bool ok2 = await transport.TryWriteLineAsync("isready", Timeout.InfiniteTimeSpan);
			ok2.Should().BeTrue();

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task TryWriteLineAsync_WhenCanceledDuringWait_ThrowsOperationCanceledException()
		{
			var options = new ProcessUciTransportOptions
			{
				ChannelCapacity  = 1,
				DisableWriteLoop = true, // force channel to stay full
				ValidateCommands = true
			};

			var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH, null, null, options);
			await transport.StartAsync();

			// Fill the channel
			bool ok1 = await transport.TryWriteLineAsync("uci", TimeSpan.FromMilliseconds(100));
			ok1.Should().BeTrue();

			// Second write blocks -> cancel the token to hit OperationCanceledException branch
			var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
			await FluentActions
				  .Awaiting(() => transport.TryWriteLineAsync("isready", TimeSpan.FromSeconds(5), cts.Token))
				  .Should()
				  .ThrowAsync<OperationCanceledException>();

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task TryWriteLineAsync_WhenChannelReady_ReturnsTrue()
		{
			const string path    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(path);
			await process.StartAsync();

			bool ok = await process.TryWriteLineAsync("uci", TimeSpan.FromSeconds(1));
			ok.Should().BeTrue();

			await process.DisposeAsync();
		}

		[Fact]
		public async Task TryWriteLineAsync_WhenProcessHasExited_ThrowsInvalidOperationException()
		{
			const string cmdPath = TestConsts.STOCKFISH_PATH;
			var transport = new ProcessUciTransport(
				cmdPath,
				[ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, ProcessArgs.ZERO]);

			var exitedTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
			transport.Exited += (_, _) => exitedTcs.TrySetResult(null);

			await transport.StartAsync();

			var completed = await Task.WhenAny(exitedTcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
			completed.Should().Be(exitedTcs.Task);

			await FluentActions.Awaiting(() => transport.TryWriteLineAsync("uci", TimeSpan.FromMilliseconds(10)))
							   .Should()
							   .ThrowAsync<InvalidOperationException>();
		}

		[Fact]
		public async Task TryWriteLineAsync_WithTimeout_ShouldReturnFalseAfterTimeout()
		{
			var options = new ProcessUciTransportOptions
			{
				ChannelCapacity  = 1,
				DisableWriteLoop = true
			};

			var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH, null, null, options);
			await transport.StartAsync();

			bool firstOk = await transport.TryWriteLineAsync("uci", TimeSpan.FromMilliseconds(50));
			firstOk.Should().BeTrue();

			bool ok = await transport.TryWriteLineAsync("isready", TimeSpan.FromMilliseconds(100));
			ok.Should().BeFalse();

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task WhenProcessExits_ExitedEventRaisedOnce_WithExitCode()
		{
			string? cmdPath = TryResolveCmdPath();
			if (cmdPath is null) return;

			var transport = new ProcessUciTransport(
				cmdPath,
				[ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, ProcessArgs.ZERO]);

			var tcs = new TaskCompletionSource<(int? Code, string? Error)>(
				TaskCreationOptions.RunContinuationsAsynchronously);

			var count = 0;
			transport.Exited += (code, error) =>
			{
				Interlocked.Increment(ref count);
				tcs.TrySetResult((code, error));
			};

			await transport.StartAsync();

			var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
			completed.Should().Be(tcs.Task);

			var result = await tcs.Task;
			result.Code.HasValue.Should().BeTrue();
			result.Code!.Value.Should().Be(0);
			result.Error.Should().BeNull();
			count.Should().Be(1);

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task WriteLineAsync_AfterDispose_ThrowsInvalidOperationException()
		{
			const string path    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(path);
			await process.StartAsync();
			await process.DisposeAsync();

			await FluentActions.Awaiting(() => process.WriteLineAsync("uci"))
							   .Should()
							   .ThrowAsync<InvalidOperationException>();
		}

		[Fact]
		public async Task WriteLineAsync_AfterStop_ThrowsInvalidOperationException()
		{
			const string path    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(path);
			await process.StartAsync();
			await process.StopAsync();

			await FluentActions.Awaiting(() => process.WriteLineAsync("uci"))
							   .Should()
							   .ThrowAsync<InvalidOperationException>();
		}

		[Fact]
		public async Task WriteLineAsync_WhenCalledWithEngineStart_WritesLineToEngine()
		{
			const string    path    = TestConsts.STOCKFISH_PATH;
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
			const string path    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(path);
			await process.StartAsync();

			await FluentActions
				  .Awaiting(() => process.WriteLineAsync("uci\nisready"))
				  .Should()
				  .ThrowAsync<ArgumentException>();

			await process.DisposeAsync();
		}

		[Fact]
		public async Task WriteLineAsync_WhenCalledWithNull_ThrowsArgumentNullException()
		{
			const string path    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(path);
			await process.StartAsync();

			// ReSharper disable once AssignNullToNotNullAttribute
			await FluentActions
				  .Awaiting(() => process.WriteLineAsync(null!))
				  .Should()
				  .ThrowAsync<ArgumentNullException>();

			await process.DisposeAsync();
		}

		[Fact]
		public async Task WriteLineAsync_WhenCalledWithWhitespace_ThrowsArgumentException()
		{
			const string path    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(path);
			await process.StartAsync();

			await FluentActions
				  .Awaiting(() => process.WriteLineAsync("   "))
				  .Should()
				  .ThrowAsync<ArgumentException>();

			await process.DisposeAsync();
		}

		[Fact]
		public async Task WriteLineAsync_WhenCanceled_ThrowsOperationCanceledException()
		{
			const string path    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(path);
			await process.StartAsync();

			var cts = new CancellationTokenSource();
			// ReSharper disable once MethodHasAsyncOverload
			cts.Cancel();

			await FluentActions.Awaiting(() => process.WriteLineAsync("uci", cts.Token))
							   .Should()
							   .ThrowAsync<OperationCanceledException>();

			await process.DisposeAsync();
		}

		[Fact]
		public async Task WriteLineAsync_WhenChannelFull_ShouldBlockThenSucceed()
		{
			var options = new ProcessUciTransportOptions
			{
				ChannelCapacity  = 1,
				DisableWriteLoop = true
			};

			var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH, null, null, options);
			await transport.StartAsync();

			// Fill the channel once
			await transport.WriteLineAsync("uci");

			// Next write should block until drained
			var secondWrite = transport.WriteLineAsync("isready");

			var completedEarly = await Task.WhenAny(secondWrite, Task.Delay(50));
			completedEarly.Should().NotBe(secondWrite);

			// Drain one item from the private _outgoing channel to free capacity
			var outgoingField = typeof(ProcessUciTransport).GetField(
				"_outgoing",
				BindingFlags.Instance | BindingFlags.NonPublic);

			outgoingField.Should().NotBeNull();

			var channel = (Channel<string>?)outgoingField!.GetValue(transport);
			channel.Should().NotBeNull();

			bool drained = channel!.Reader.TryRead(out _);
			drained.Should().BeTrue();

			// Now the blocked write should complete
			await secondWrite;

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task WriteLineAsync_WhenProcessHasExited_ThrowsInvalidOperationException()
		{
			var transport = new ProcessUciTransport(
				TestConsts.STOCKFISH_PATH,
				[ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, ProcessArgs.ZERO]);

			var exitedTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
			transport.Exited += (_, _) => exitedTcs.TrySetResult(null);

			await transport.StartAsync();

			// Wait for the process to actually exit to hit the HasExited guard reliably
			var completed = await Task.WhenAny(exitedTcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
			completed.Should().Be(exitedTcs.Task);

			await FluentActions.Awaiting(() => transport.WriteLineAsync("uci"))
							   .Should()
							   .ThrowAsync<InvalidOperationException>();

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task WriteLineAsync_WithUnknownCommand_DoesNotThrowAndReadingContinues()
		{
			const string path    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(path);
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
			await process.DisposeAsync();
		}

		[Fact]
		public async Task WriteLineAsync_WithValidationDisabled_AllowsWhitespaceAndNewline()
		{
			const string path    = TestConsts.STOCKFISH_PATH;
			var          options = new ProcessUciTransportOptions { ValidateCommands = false };
			var          process = new ProcessUciTransport(path, null, null, options);
			await process.StartAsync();

			// Should not throw even though input contains whitespace-only or newline
			await FluentActions.Awaiting(() => process.WriteLineAsync("   "))
							   .Should()
							   .NotThrowAsync();

			await FluentActions.Awaiting(() => process.WriteLineAsync("uci\n"))
							   .Should()
							   .NotThrowAsync();

			await process.DisposeAsync();
		}
	}

	public class Performance
	{
		[Fact]
		public async Task DisposeAsync_WhenExitNotificationStuck_CompletesWithinTeardownTimeout()
		{
			const string path = TestConsts.STOCKFISH_PATH;
			var options = new ProcessUciTransportOptions
			{
				TeardownTimeout = TimeSpan.FromMilliseconds(200)
			};

			var transport = new ProcessUciTransport(path, null, null, options);
			await transport.StartAsync();

			// Replace the internal _exitNotifyTask with a never-completing task to simulate a stuck exit notification.
			var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
			var field = typeof(ProcessUciTransport).GetField(
				"_exitNotifyTask",
				BindingFlags.Instance | BindingFlags.NonPublic);

			field.Should().NotBeNull();
			field!.SetValue(transport, tcs.Task);

			var sw = Stopwatch.StartNew();
			await transport.DisposeAsync();
			sw.Stop();

			// Should not hang; should complete reasonably quickly (bounded by TeardownTimeout with slack).
			sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));

			await transport.DisposeAsync();
		}
	}

	public class Unit
	{
		[Fact]
		public async Task Dispose_ThenDisposeAsync_IsIdempotent()
		{
			var process = new ProcessUciTransport("any/nonempty/path");
			// ReSharper disable once MethodHasAsyncOverload
			process.Dispose();

			await process.Awaiting(p => p.DisposeAsync().AsTask()).Should().NotThrowAsync();
		}

		[Fact]
		public async Task DisposeAsync_BeforeStart_IsNoOpAndDoesNotThrow()
		{
			await using var process = new ProcessUciTransport("any/nonempty/path");
			await process.Awaiting(p => p.DisposeAsync().AsTask()).Should().NotThrowAsync();
			process.IsStarted.Should().BeFalse();
			process.Status.Should().Be(ProcessUciTransport.TransportStatus.Disposed);
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
		public async Task StartAsync_AfterDispose_ThrowsObjectDisposedException()
		{
			var process = new ProcessUciTransport("any/nonempty/path");
			await process.DisposeAsync();

			await FluentActions.Awaiting(() => process.StartAsync())
							   .Should()
							   .ThrowAsync<ObjectDisposedException>();
		}

		[Fact]
		public async Task StartAsync_WhenCalledWithInvalidProcess_ThrowsException()
		{
			var process = new ProcessUciTransport("invalid/path");

			await FluentActions.Awaiting(() => process.StartAsync()).Should().ThrowAsync<Exception>();
		}

		[Fact]
		public async Task StartAsync_WhenCanceled_ThrowsAndLeavesCleanState()
		{
			var process = new ProcessUciTransport("any/nonempty/path");
			var cts     = new CancellationTokenSource();
			// ReSharper disable once MethodHasAsyncOverload
			cts.Cancel();

			await FluentActions.Awaiting(() => process.StartAsync(cts.Token))
							   .Should()
							   .ThrowAsync<OperationCanceledException>();

			process.IsStarted.Should().BeFalse();
		}

		[Fact]
		public async Task StartAsync_WhenExistingStartInProgress_Canceled_ThrowsOperationCanceledException()
		{
			var transport = new ProcessUciTransport("any/nonempty/path");

			// Simulate a concurrent Start in progress by publishing a never-completing _startingTcs
			var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
			var startingTcsField = typeof(ProcessUciTransport).GetField(
				"_startingTcs",
				BindingFlags.Instance | BindingFlags.NonPublic);

			startingTcsField.Should().NotBeNull();
			startingTcsField!.SetValue(transport, tcs);

			var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
			await FluentActions.Awaiting(() => transport.StartAsync(cts.Token))
							   .Should()
							   .ThrowAsync<OperationCanceledException>();
		}

		[Fact]
		public async Task StartAsync_WithMissingPath_ShouldThrowAndCleanup()
		{
			string missing = Path.Combine(
				Path.GetTempPath(),
				"no-such-dir",
				Guid.NewGuid().ToString("N"),
				"missing.exe");

			var transport = new ProcessUciTransport(missing);

			await FluentActions.Awaiting(() => transport.StartAsync())
							   .Should()
							   .ThrowAsync<ArgumentException>();

			transport.IsStarted.Should().BeFalse();
			transport.Status.Should().Be(ProcessUciTransport.TransportStatus.Failed);

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task StopAsync_AfterDispose_ThrowsObjectDisposedException()
		{
			var process = new ProcessUciTransport("any/nonempty/path");
			await process.DisposeAsync();

			await FluentActions.Awaiting(() => process.StopAsync())
							   .Should()
							   .ThrowAsync<ObjectDisposedException>();
		}

		[Fact]
		public async Task StopAsync_DuringStart_ShouldWaitForStartThenStop()
		{
			var transport = new ProcessUciTransport("any/nonempty/path");
			var tcs       = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

			var startingTcsField = typeof(ProcessUciTransport).GetField(
				"_startingTcs",
				BindingFlags.Instance | BindingFlags.NonPublic);

			startingTcsField.Should().NotBeNull();
			startingTcsField!.SetValue(transport, tcs);

			var stopTask = transport.StopAsync();

			var notDone = await Task.WhenAny(stopTask, Task.Delay(50));
			notDone.Should().NotBe(stopTask);

			tcs.TrySetResult(null);

			await stopTask;

			transport.Status.Should().Be(ProcessUciTransport.TransportStatus.Stopped);

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task StopAsync_WhenCanceled_ThrowsOperationCanceledException_AndLeavesStateUnchanged()
		{
			var process = new ProcessUciTransport("any/nonempty/path");
			var cts     = new CancellationTokenSource();
			// ReSharper disable once MethodHasAsyncOverload
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
		public async Task
			StopAsync_WhenStartInProgress_Canceled_ThrowsOperationCanceledException_AndLeavesStateUnchanged()
		{
			var transport = new ProcessUciTransport("any/nonempty/path");

			// Simulate a concurrent Start in progress by publishing a never-completing _startingTcs
			var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
			var startingTcsField = typeof(ProcessUciTransport).GetField(
				"_startingTcs",
				BindingFlags.Instance | BindingFlags.NonPublic);

			startingTcsField.Should().NotBeNull();
			startingTcsField!.SetValue(transport, tcs);

			var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
			await FluentActions.Awaiting(() => transport.StopAsync(cts.Token))
							   .Should()
							   .ThrowAsync<OperationCanceledException>();

			transport.Status.Should().Be(ProcessUciTransport.TransportStatus.Created);
		}

		[Fact]
		public async Task TryWriteLineAsync_NullLine_ThrowsArgumentNullException()
		{
			var process = new ProcessUciTransport("any/nonempty/path");

			await FluentActions
				  .Awaiting(() => process.TryWriteLineAsync(null!, TimeSpan.FromMilliseconds(10)))
				  .Should()
				  .ThrowAsync<ArgumentNullException>();
		}

		[Fact]
		public async Task TryWriteLineAsync_WhenCalledWithoutEngineStart_ThrowsInvalidOperationException()
		{
			var process = new ProcessUciTransport("any/nonempty/path");

			await FluentActions.Awaiting(() => process.TryWriteLineAsync("uci", TimeSpan.FromMilliseconds(10)))
							   .Should()
							   .ThrowAsync<InvalidOperationException>();
		}

		[Fact]
		public async Task TryWriteLineAsync_WithNegativeNonInfiniteTimeout_ThrowsArgumentOutOfRangeException()
		{
			var process = new ProcessUciTransport("any/nonempty/path");

			await FluentActions.Awaiting(() => process.TryWriteLineAsync("uci", TimeSpan.FromMilliseconds(-2)))
							   .Should()
							   .ThrowAsync<ArgumentOutOfRangeException>();
		}

		[Fact]
		public async Task WriteLineAsync_WhenCalledWithCarriageReturn_ThrowsArgumentException()
		{
			var process = new ProcessUciTransport("any/nonempty/path");

			await FluentActions.Awaiting(() => process.WriteLineAsync("uci\r"))
							   .Should()
							   .ThrowAsync<ArgumentException>();
		}

		[Fact]
		public async Task WriteLineAsync_WhenCalledWithoutEngineStart_ThrowsException()
		{
			var process = new ProcessUciTransport("any/nonempty/path");

			await FluentActions
				  .Awaiting(() => process.WriteLineAsync("uci"))
				  .Should()
				  .ThrowAsync<InvalidOperationException>();
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
	}
}
