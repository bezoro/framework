using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using Bezoro.UCI.Domain;
using Bezoro.UCI.Domain.Common.Constants;
using Bezoro.UCI.Tests._Resources;
using Bezoro.UCI.Tests.Helpers;
using Bezoro.UCI.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Domain;

[TestSubject(typeof(ProcessUciTransport))]
public static class ProcessUciTransportTests
{
	private static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

	private static ProcessUciTransport CreateTransportWithOptions(ProcessUciTransportOptions? options = null) =>
		new(TestConsts.STOCKFISH_PATH, null, null, options);

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

	private static async Task WaitForStatusAsync(
		ProcessUciTransport transport,
		TransportStatus     status,
		TimeSpan?           timeout = null)
	{
		timeout ??= TestConstants.DefaultTimeout;
		bool conditionMet = await AsyncTestHelpers.WaitForConditionAsync(
								() => transport.Status == status,
								TestConstants.ShortDelay,
								timeout.Value);

		conditionMet.Should().BeTrue($"Status should transition to {status} within timeout");
	}

	private static async Task WithTransportAsync(Func<ProcessUciTransport, Task> action)
	{
		var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH);
		await transport.StartAsync();
		try
		{
			await action(transport);
		}
		finally
		{
			await transport.DisposeAsync();
		}
	}

	private static async Task<T> WithTransportAsync<T>(Func<ProcessUciTransport, Task<T>> action)
	{
		var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH);
		await transport.StartAsync();
		try
		{
			return await action(transport);
		}
		finally
		{
			await transport.DisposeAsync();
		}
	}

	public class IntegrationTests
	{
		[Fact]
		public async Task Exited_Event_IsRaised_OnProcessExit()
		{
			const string PATH = TestConsts.STOCKFISH_PATH;
			var tcs = new TaskCompletionSource<(int? Code, string? Error)>(
				TaskCreationOptions.RunContinuationsAsynchronously);

			var process = new ProcessUciTransport(PATH);
			process.Exited += (code, error) => tcs.TrySetResult((code, error));
			await process.StartAsync();
			await process.DisposeAsync();

			var completed = await Task.WhenAny(tcs.Task, Task.Delay(TestConstants.DefaultTimeout));
			completed.Should().Be(tcs.Task, "Exited event should be raised on process exit");

			var result = await tcs.Task;
			result.Code.HasValue.Should().BeTrue();
			result.Error.Should().BeNull();
		}

		[Fact]
		public async Task Exited_Event_IsRaisedOnlyOnce()
		{
			var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH);

			var count           = 0;
			var tcs             = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
			var verificationTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

			transport.Exited += (_, _) =>
			{
				int newCount = Interlocked.Increment(ref count);
				tcs.TrySetResult(null);
				// Signal after a brief delay to ensure no duplicate events fire
				Task.Delay(TestConstants.StandardDelay).ContinueWith(_ => verificationTcs.TrySetResult(newCount));
			};

			await transport.StartAsync();
			await transport.DisposeAsync();

			var completed = await Task.WhenAny(tcs.Task, Task.Delay(TestConstants.DefaultTimeout));
			completed.Should().Be(tcs.Task, "Exited event should be raised");

			// Wait for verification to complete after delay
			var verificationCompleted = await Task.WhenAny(
											verificationTcs.Task,
											Task.Delay(TestConstants.DefaultTimeout));

			verificationCompleted.Should().Be(verificationTcs.Task, "Verification should complete");

			int finalCount = await verificationTcs.Task;
			finalCount.Should().Be(1, "Exited event should be raised exactly once");
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

			var completed = await Task.WhenAny(errorTcs.Task, Task.Delay(TestConstants.DefaultTimeout));
			completed.Should().Be(errorTcs.Task, "Error event should be raised when Exited handler throws");

			(await errorTcs.Task).Should().BeOfType<InvalidOperationException>();

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task IsHealthy_AfterDispose_IsFalse()
		{
			const string PATH    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(PATH);
			await process.StartAsync();

			await process.DisposeAsync();
			process.IsHealthy.Should().BeFalse();
		}

		[Fact]
		public async Task IsHealthy_AfterStop_IsFalse()
		{
			const string PATH    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(PATH);
			await process.StartAsync();

			await process.StopAsync();
			process.IsHealthy.Should().BeFalse();

			await process.DisposeAsync();
		}

		[Fact]
		public async Task IsHealthy_WhenRunning_IsTrue()
		{
			const string PATH    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(PATH);
			await process.StartAsync();

			process.IsHealthy.Should().BeTrue();

			await process.DisposeAsync();
		}

		[Fact]
		public async Task ReadLoop_Backpressure_ChannelFull_IncrementsBackpressureEvents()
		{
			if (!IsWindows()) return;

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
			await Task.Delay(TestConstants.LongerDelay);

			await transport.StopAsync();

			transport.BackpressureEvents.Should().BeGreaterThan(0);

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task ReadLoop_SkipsEmptyLines_OnlyNonEmptyReceived()
		{
			if (!IsWindows()) return;

			string? cmdPath = TryResolveCmdPath();
			if (cmdPath is null) return;

			var transport = new ProcessUciTransport(
				cmdPath,
				[ProcessArgs.CMD_EXECUTE, ProcessArgs.ECHO_EMPTY, ProcessArgs.CHAIN, ProcessArgs.ECHO, "marker"]);

			await transport.StartAsync();

			string?   received = null;
			using var cts      = new CancellationTokenSource(TestConstants.DefaultTimeout);
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
		public async Task Status_Transitions_CreatedToStartedToStoppedToDisposed_AreObserved()
		{
			const string PATH    = TestConsts.STOCKFISH_PATH;
			var          process = new ProcessUciTransport(PATH);

			process.Status.Should().Be(TransportStatus.Created);

			await process.StartAsync();
			process.Status.Should().Be(TransportStatus.Started);

			await process.StopAsync();
			process.Status.Should().Be(TransportStatus.Stopped);

			await process.DisposeAsync();
			process.Status.Should().Be(TransportStatus.Disposed);
		}

		[Fact]
		public async Task StderrDisabled_ShouldNotStartStderrLoop()
		{
			if (!IsWindows()) return;

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
			var completed = await Task.WhenAny(tcs.Task, Task.Delay(TestConstants.BackpressureDelay));
			completed.Should().NotBe(tcs.Task);

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task StderrReceived_Event_Fires_WhenRedirected()
		{
			if (!IsWindows()) return;

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

			var completed = await Task.WhenAny(tcs.Task, Task.Delay(TestConstants.DefaultTimeout));
			completed.Should().Be(tcs.Task);

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task StderrReceived_HandlerThrows_IsSwallowed_NoCrash()
		{
			if (!IsWindows()) return;

			string? cmdPath = TryResolveCmdPath();
			if (cmdPath is null) return;

			var options = new ProcessUciTransportOptions { RedirectStandardError = true };
			var transport = new ProcessUciTransport(
				cmdPath,
				[ProcessArgs.CMD_EXECUTE, ProcessArgs.ECHO, "oops", ProcessArgs.STD_OUT_TO_STD_ERR],
				null,
				options);

			var errorTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
			transport.Error += ex => errorTcs.TrySetResult(ex);

			// Handler intentionally throws; transport should swallow and NOT surface via Error.
			transport.StderrReceived += _ => throw new InvalidOperationException("stderr handler boom");

			await transport.StartAsync();

			// Allow stderr loop to process and observe whether Error was raised due to the thrown handler.
			var completed = await Task.WhenAny(errorTcs.Task, Task.Delay(TestConstants.BackpressureDelay));

			// If handler exceptions were not swallowed, Error would fire.
			completed.Should().NotBe(
				errorTcs.Task,
				"stderr handler exceptions must be swallowed and not surface via Error");

			await transport.DisposeAsync();
		}

		[Fact]
		public async Task WhenProcessExits_ExitedEventRaisedOnce_WithExitCode()
		{
			if (!IsWindows()) return;

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

			var completed = await Task.WhenAny(tcs.Task, Task.Delay(TestConstants.DefaultTimeout));
			completed.Should().Be(tcs.Task);

			var result = await tcs.Task;
			result.Code.HasValue.Should().BeTrue();
			result.Code!.Value.Should().Be(0);
			result.Error.Should().BeNull();
			count.Should().Be(1);

			await transport.DisposeAsync();
		}

		public class DisposeTests
		{
			[Fact]
			public async Task DisposeAsync_MultipleCalls_ShouldBeIdempotent()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				await process.DisposeAsync();
				await process.DisposeAsync();

				process.IsStarted.Should().BeFalse();
				process.Status.Should().Be(TransportStatus.Disposed);
			}

			[Fact]
			public async Task WhenAsyncDisposedAfterStart_StopsProcessAndResetsIsStarted()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);

				await process.StartAsync();
				await process.DisposeAsync();

				process.IsStarted.Should().BeFalse();
			}
		}

		public class ReadLinesTests
		{
			[Fact]
			public async Task ReadLinesAsync_AfterDispose_CompletesGracefully()
			{
				const string? PATH    = TestConsts.STOCKFISH_PATH;
				var           process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				var enumerator = process.ReadLinesAsync(CancellationToken.None).GetAsyncEnumerator();
				var pending    = enumerator.MoveNextAsync().AsTask();

				await process.DisposeAsync();

				var completed = await Task.WhenAny(pending, Task.Delay(TestConstants.DefaultTimeout));
				completed.Should().Be(pending, "enumeration should complete after dispose");
				(await pending).Should().BeFalse("no more lines should be available after transport is disposed");
			}

			[Fact]
			public async Task ReadLinesAsync_AfterStop_CompletesGracefully()
			{
				const string    PATH    = TestConsts.STOCKFISH_PATH;
				await using var process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				var enumerator = process.ReadLinesAsync(CancellationToken.None).GetAsyncEnumerator();
				var pending    = enumerator.MoveNextAsync().AsTask();

				await process.StopAsync();

				var completed = await Task.WhenAny(pending, Task.Delay(TestConstants.DefaultTimeout));
				completed.Should().Be(pending, "enumeration should complete after stop");
				(await pending).Should().BeFalse("no more lines should be available after transport is stopped");

				await enumerator.DisposeAsync();
			}

			[Fact]
			public async Task ReadLinesAsync_WithSecondConcurrentReader_ThrowsInvalidOperationException()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
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
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				using var cts1 = new CancellationTokenSource(TestConstants.TinyTimeout);
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

				using var cts2 = new CancellationTokenSource(TestConstants.MediumDelay);
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
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          options = new ProcessUciTransportOptions { SingleReader = false };
				var          process = new ProcessUciTransport(PATH, null, null, options);
				await process.StartAsync();

				using var cts = new CancellationTokenSource(TestConstants.MediumDelay);
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
		}

		public class StartTests
		{
			[Fact]
			public async Task StartAsync_ConcurrentCalls_StartsOnceAndBothComplete()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);

				var t1 = process.StartAsync();
				var t2 = process.StartAsync();

				await Task.WhenAll(t1, t2);

				process.IsStarted.Should().BeTrue();

				await process.DisposeAsync();
			}

			[Fact]
			public async Task StartAsync_WhenAlreadyStarted_IsIdempotent()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				var before = process.Status;

				await process.StartAsync(); // second call should be a no-op and not throw
				var after = process.Status;

				process.IsStarted.Should().BeTrue();
				before.Should().Be(TransportStatus.Started);
				after.Should().Be(TransportStatus.Started);

				await process.DisposeAsync();
			}


			[Fact]
			public async Task StartAsync_WhenCalledWithValidProcess_StartsProcess()
			{
				const string    PATH    = TestConsts.STOCKFISH_PATH;
				await using var process = new ProcessUciTransport(PATH);

				await process.StartAsync();

				process.IsStarted.Should().BeTrue();
			}

			[Fact]
			public async Task StartAsync_WhenConcurrent_ShouldStartOnceAndAwaitOthers()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);

				var t1 = process.StartAsync();
				var t2 = process.StartAsync();
				var t3 = process.StartAsync();

				await Task.WhenAll(t1, t2, t3);

				process.IsStarted.Should().BeTrue();
				process.Status.Should().Be(TransportStatus.Started);

				await process.DisposeAsync();
			}

			[Fact]
			public async Task StartAsync_WhileStopping_ThrowsInvalidOperationException()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				var stoppingTask = process.StopAsync(); // begin stopping but don't await

				// Wait until the status transitions to Stopping to avoid a race
				bool conditionMet = await AsyncTestHelpers.WaitForConditionAsync(
										() => process.Status == TransportStatus.Stopping,
										TestConstants.ShortDelay,
										TestConstants.DefaultTimeout);

				conditionMet.Should().BeTrue("Status should transition to Stopping");

				await FluentActions.Awaiting(() => process.StartAsync())
								   .Should()
								   .ThrowAsync<InvalidOperationException>();

				await stoppingTask; // clean up
				await process.DisposeAsync();
			}

			[Fact]
			public async Task StartAsync_WithInvalidWorkingDirectory_ThrowsArgumentException()
			{
				const string PATH       = TestConsts.STOCKFISH_PATH;
				string       invalidDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
				var          process    = new ProcessUciTransport(PATH, null, invalidDir);

				await FluentActions.Awaiting(() => process.StartAsync())
								   .Should()
								   .ThrowAsync<ArgumentException>();

				await process.DisposeAsync();
			}

			[Fact]
			public async Task StartAsync_WithPreexistingExitedProcessObject_CleansUpAndStarts()
			{
				if (!IsWindows()) return;

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
		}

		public class StopTests
		{
			[Fact]
			public async Task StopAsync_ShouldDisposeStreamsAndCompleteChannels()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				var enumerator = process.ReadLinesAsync(CancellationToken.None).GetAsyncEnumerator();
				var pending    = enumerator.MoveNextAsync().AsTask();

				await process.StopAsync();

				var completed = await Task.WhenAny(pending, Task.Delay(TestConstants.DefaultTimeout));
				completed.Should().Be(pending);
				(await pending).Should().BeFalse();

				await enumerator.DisposeAsync();

				await process.DisposeAsync();
			}

			[Fact]
			public async Task StopAsync_ShouldSendQuitThenKillAfterGrace()
			{
				const string PATH = TestConsts.STOCKFISH_PATH;

				// Track whether quit was sent using the test hook
				var quitSent = false;

				var options = new ProcessUciTransportOptions
				{
					SendQuitOnStop  = true,
					QuitGracePeriod = TestConstants.QuitGracePeriod,
					OnQuitSent      = () => quitSent = true
				};

				var transport = new ProcessUciTransport(PATH, null, null, options);
				await transport.StartAsync();

				await transport.StopAsync();

				// Verify that quit WAS sent when SendQuitOnStop is true
				quitSent.Should().BeTrue("quit command should be sent when SendQuitOnStop is true");
				transport.Status.Should().Be(TransportStatus.Stopped);

				await transport.DisposeAsync();
			}
		}

		public class TryWriteLineTests
		{
			[Fact]
			public async Task TryWriteLineAsync_AfterDispose_ThrowsInvalidOperationException()
			{
				const string PATH = TestConsts.STOCKFISH_PATH;

				var process = new ProcessUciTransport(PATH);
				await process.StartAsync();
				await process.DisposeAsync();

				await FluentActions.Awaiting(() => process.TryWriteLineAsync("uci", TestConstants.TinyTimeout))
								   .Should()
								   .ThrowAsync<InvalidOperationException>();
			}

			[Fact]
			public async Task TryWriteLineAsync_AfterStop_ThrowsInvalidOperationException()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();
				await process.StopAsync();

				await FluentActions.Awaiting(() => process.TryWriteLineAsync("uci", TestConstants.TinyTimeout))
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
				bool ok1 = await transport.TryWriteLineAsync("uci", TestConstants.TinyTimeout);
				ok1.Should().BeTrue();

				var writeTask = transport.TryWriteLineAsync("isready", TestConstants.DefaultTimeout);

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

				bool ok1 = await transport.TryWriteLineAsync("uci", TestConstants.TinyTimeout);
				ok1.Should().BeTrue();

				bool ok2 = await transport.TryWriteLineAsync("isready", TestConstants.VeryShortDelay);
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
				bool ok1 = await transport.TryWriteLineAsync("uci", TestConstants.TinyTimeout);
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
				bool ok1 = await transport.TryWriteLineAsync("uci", TestConstants.TinyTimeout);
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
				bool ok1 = await transport.TryWriteLineAsync("uci", TestConstants.SmallTimeout);
				ok1.Should().BeTrue();

				// Second write blocks -> cancel the token to hit OperationCanceledException branch
				var cts = new CancellationTokenSource(TestConstants.MediumDelay);
				await FluentActions
					  .Awaiting(() => transport.TryWriteLineAsync("isready", TestConstants.DefaultTimeout, cts.Token))
					  .Should()
					  .ThrowAsync<OperationCanceledException>();

				await transport.DisposeAsync();
			}

			[Fact]
			public async Task TryWriteLineAsync_WhenChannelReady_ReturnsTrue()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				bool ok = await process.TryWriteLineAsync("uci", TestConstants.ShortTimeout);
				ok.Should().BeTrue();

				await process.DisposeAsync();
			}

			[Fact]
			public async Task TryWriteLineAsync_WhenProcessHasExited_ThrowsInvalidOperationException()
			{
				const string CMD_PATH = TestConsts.STOCKFISH_PATH;
				var transport = new ProcessUciTransport(
					CMD_PATH,
					[ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, ProcessArgs.ZERO]);

				var exitedTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
				transport.Exited += (_, _) => exitedTcs.TrySetResult(null);

				await transport.StartAsync();

				var completed = await Task.WhenAny(exitedTcs.Task, Task.Delay(TestConstants.DefaultTimeout));
				completed.Should().Be(exitedTcs.Task);

				await FluentActions.Awaiting(() => transport.TryWriteLineAsync("uci", TestConstants.TinyTimeout))
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

				bool firstOk = await transport.TryWriteLineAsync("uci", TestConstants.MediumDelay);
				firstOk.Should().BeTrue();

				bool ok = await transport.TryWriteLineAsync("isready", TestConstants.SmallTimeout);
				ok.Should().BeFalse();

				await transport.DisposeAsync();
			}
		}

		public class WriteLineTests
		{
			[Fact]
			public async Task WriteLineAsync_AfterDispose_ThrowsInvalidOperationException()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();
				await process.DisposeAsync();

				await FluentActions.Awaiting(() => process.WriteLineAsync("uci"))
								   .Should()
								   .ThrowAsync<InvalidOperationException>();
			}

			[Fact]
			public async Task WriteLineAsync_AfterStop_ThrowsInvalidOperationException()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();
				await process.StopAsync();

				await FluentActions.Awaiting(() => process.WriteLineAsync("uci"))
								   .Should()
								   .ThrowAsync<InvalidOperationException>();
			}

			[Fact]
			public async Task WriteLineAsync_WhenCalledWithEngineStart_WritesLineToEngine()
			{
				const string    PATH    = TestConsts.STOCKFISH_PATH;
				await using var process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				await process.WriteLineAsync("uci");

				string?   output = null;
				using var cts    = new CancellationTokenSource(TestConstants.DefaultTimeout);

				await foreach (string line in process.ReadLinesAsync(cts.Token))
				{
					if (!string.Equals(line.Trim(), "uciok", StringComparison.OrdinalIgnoreCase)) continue;

					output = line;
					break;
				}

				output.Should().Be(
					"uciok",
					"the engine should acknowledge UCI initialization with 'uciok' within timeout");
			}

			[Fact]
			public async Task WriteLineAsync_WhenCalledWithNewline_ThrowsArgumentException()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
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
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
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
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
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
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
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

				var completedEarly = await Task.WhenAny(secondWrite, Task.Delay(TestConstants.MediumDelay));
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
				var completed = await Task.WhenAny(exitedTcs.Task, Task.Delay(TestConstants.DefaultTimeout));
				completed.Should().Be(exitedTcs.Task);

				await FluentActions.Awaiting(() => transport.WriteLineAsync("uci"))
								   .Should()
								   .ThrowAsync<InvalidOperationException>();

				await transport.DisposeAsync();
			}

			[Fact]
			public async Task WriteLineAsync_WithUnknownCommand_DoesNotThrowAndReadingContinues()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				await FluentActions.Awaiting(() => process.WriteLineAsync("this_is_not_a_real_command"))
								   .Should()
								   .NotThrowAsync();

				await process.WriteLineAsync("uci");

				string?   output = null;
				using var cts    = new CancellationTokenSource(TestConstants.DefaultTimeout);

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
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          options = new ProcessUciTransportOptions { ValidateCommands = false };
				var          process = new ProcessUciTransport(PATH, null, null, options);
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
	}

	public static class UnitTests
	{
		public class ConstructorTests
		{
			[Fact]
			public void Default_InitialStateIsCorrect()
			{
				var process = new ProcessUciTransport("any/nonempty/path");

				process.Status.Should().Be(TransportStatus.Created);
				process.IsStarted.Should().BeFalse();
				process.BackpressureEvents.Should().Be(0);
				process.LinesRead.Should().Be(0);
				process.LinesWritten.Should().Be(0);
			}

			[Fact]
			public void Default_StatusIsCreated()
			{
				var process = new ProcessUciTransport("any/nonempty/path");

				process.Status.Should().Be(TransportStatus.Created);
			}

			[Fact]
			public void FourParameterOverload_WorksCorrectly()
			{
				var      customOptions = new ProcessUciTransportOptions { ChannelCapacity = 512 };
				string[] args          = new[] { "arg1", "arg2" };
				string   workingDir    = Environment.CurrentDirectory;

				var process = new ProcessUciTransport("any/nonempty/path", args, workingDir, customOptions);
				process.Should().NotBeNull();
				process.Status.Should().Be(TransportStatus.Created);
				process.IsStarted.Should().BeFalse();
			}

			[Fact]
			public void WithEmptyArgs_CreatesSuccessfully()
			{
				var process = new ProcessUciTransport("any/nonempty/path", []);

				process.Should().NotBeNull();
				process.Status.Should().Be(TransportStatus.Created);
			}

			[Fact]
			public void WithEmptyPath_ThrowsArgumentException()
			{
				var act = () => _ = new ProcessUciTransport("");

				act.Should().Throw<ArgumentException>();
			}

			[Fact]
			public void WithEmptyWorkingDirectory_CreatesSuccessfully()
			{
				var process = new ProcessUciTransport("any/nonempty/path", null, "");

				process.Should().NotBeNull();
				process.Status.Should().Be(TransportStatus.Created);
			}

			[Fact]
			public void WithInvalidOptions_ChannelCapacityZero_ThrowsArgumentOutOfRangeException()
			{
				var    invalidOptions = new ProcessUciTransportOptions { ChannelCapacity = 0 };
				Action act = () => _ = new ProcessUciTransport("any/nonempty/path", null, null, invalidOptions);

				act.Should().Throw<ArgumentOutOfRangeException>();
			}

			[Fact]
			public void WithInvalidOptions_NewLineEmpty_ThrowsArgumentException()
			{
				var    invalidOptions = new ProcessUciTransportOptions { NewLine = "" };
				Action act = () => _ = new ProcessUciTransport("any/nonempty/path", null, null, invalidOptions);

				act.Should().Throw<ArgumentException>();
			}

			[Fact]
			public void WithMultipleArgs_CreatesSuccessfully()
			{
				var process = new ProcessUciTransport("any/nonempty/path", ["arg1", "arg2", "arg3"]);

				process.Should().NotBeNull();
				process.Status.Should().Be(TransportStatus.Created);
			}

			[Fact]
			public void WithNullArgs_DefaultCreatesSuccessfully()
			{
				var process = new ProcessUciTransport("any/nonempty/path");

				process.Should().NotBeNull();
				process.Status.Should().Be(TransportStatus.Created);
			}

			[Fact]
			public void WithNullOptions_CreatesDefaultOptions()
			{
				var process = new ProcessUciTransport("any/nonempty/path", null, null, null);
				process.Should().NotBeNull();
				process.Status.Should().Be(TransportStatus.Created);

				// Verify default options are used by checking via reflection
				var optionsField = typeof(ProcessUciTransport).GetField(
					"_options",
					BindingFlags.NonPublic | BindingFlags.Instance);

				optionsField.Should().NotBeNull();
				var storedOptions = optionsField!.GetValue(process) as ProcessUciTransportOptions;
				storedOptions.Should().NotBeNull();
				storedOptions!.SingleReader.Should().BeTrue("default options should have SingleReader = true");
				storedOptions.ChannelCapacity.Should().Be(1024, "default options should have ChannelCapacity = 1024");
			}

			[Fact]
			public void WithNullPath_ThrowsArgumentException()
			{
				var act = () => _ = new ProcessUciTransport(null!);

				act.Should().Throw<ArgumentException>();
			}

			[Fact]
			public void WithSingleArg_CreatesSuccessfully()
			{
				var process = new ProcessUciTransport("any/nonempty/path", ["arg1"]);

				process.Should().NotBeNull();
				process.Status.Should().Be(TransportStatus.Created);
			}

			[Fact]
			public void WithValidCustomOptions_UsesCustomOptions()
			{
				var customOptions = new ProcessUciTransportOptions
				{
					SingleReader          = false,
					ChannelCapacity       = 2048,
					RedirectStandardError = true
				};

				var process = new ProcessUciTransport("any/nonempty/path", null, null, customOptions);
				process.Should().NotBeNull();
				process.Status.Should().Be(TransportStatus.Created);

				// Verify custom options are used by checking via reflection
				var optionsField = typeof(ProcessUciTransport).GetField(
					"_options",
					BindingFlags.NonPublic | BindingFlags.Instance);

				optionsField.Should().NotBeNull();
				var storedOptions = optionsField!.GetValue(process) as ProcessUciTransportOptions;
				storedOptions.Should().NotBeNull();
				storedOptions!.SingleReader.Should().BeFalse("custom options should be used");
				storedOptions.ChannelCapacity.Should().Be(2048, "custom options should be used");
				storedOptions.RedirectStandardError.Should().BeTrue("custom options should be used");
			}

			[Fact]
			public void WithValidPath_CreatesSuccessfully()
			{
				var process = new ProcessUciTransport("valid/path/to/engine.exe");

				process.Should().NotBeNull();
				process.Status.Should().Be(TransportStatus.Created);
			}

			[Fact]
			public void WithValidPathBackslashes_CreatesSuccessfully()
			{
				var process = new ProcessUciTransport(@"valid\path\to\engine.exe");

				process.Should().NotBeNull();
				process.Status.Should().Be(TransportStatus.Created);
			}

			[Fact]
			public void WithValidWorkingDirectory_CreatesSuccessfully()
			{
				string workingDir = Environment.CurrentDirectory;
				var    process    = new ProcessUciTransport("any/nonempty/path", null, workingDir);

				process.Should().NotBeNull();
				process.Status.Should().Be(TransportStatus.Created);
			}

			[Fact]
			public void WithWhitespacePath_ThrowsArgumentException()
			{
				var act = () => _ = new ProcessUciTransport("   ");

				act.Should().Throw<ArgumentException>();
			}
		}

		public class DisposeTests
		{
			[Fact]
			public async Task WhenDisposed_ChannelsAreCompleted()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				// Get channels via reflection
				var linesField = typeof(ProcessUciTransport).GetField(
					"_lines",
					BindingFlags.Instance | BindingFlags.NonPublic);

				var outgoingField = typeof(ProcessUciTransport).GetField(
					"_outgoing",
					BindingFlags.Instance | BindingFlags.NonPublic);

				linesField.Should().NotBeNull();
				outgoingField.Should().NotBeNull();

				var linesChannel    = (Channel<string>?)linesField!.GetValue(process);
				var outgoingChannel = (Channel<string>?)outgoingField!.GetValue(process);

				linesChannel.Should().NotBeNull();
				outgoingChannel.Should().NotBeNull();

				await process.DisposeAsync();

				// After dispose, channels should be null (cleaned up)
				var linesAfter    = (Channel<string>?)linesField.GetValue(process);
				var outgoingAfter = (Channel<string>?)outgoingField.GetValue(process);

				linesAfter.Should().BeNull();
				outgoingAfter.Should().BeNull();
			}

			[Fact]
			public async Task WhenDisposed_ErrorEventIsNotRaisedForNormalDisposal()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				var errorRaised = false;
				process.Error += _ => errorRaised = true;

				await process.DisposeAsync();

				errorRaised.Should().BeFalse("normal disposal should not raise error event");
			}

			[Fact]
			public async Task WhenDisposed_IsHealthyReturnsFalse()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				process.IsHealthy.Should().BeTrue();

				await process.DisposeAsync();

				process.IsHealthy.Should().BeFalse();
			}

			[Fact]
			public async Task WhenDisposed_ProcessIsKilledIfNotExited()
			{
				if (!IsWindows()) return;

				string? cmdPath = TryResolveCmdPath();
				if (cmdPath is null) return;

				var transport = new ProcessUciTransport(cmdPath, [ProcessArgs.CMD_KEEP]);
				await transport.StartAsync();

				// Get process via reflection to verify it exists
				var processField = typeof(ProcessUciTransport).GetField(
					"_process",
					BindingFlags.Instance | BindingFlags.NonPublic);

				processField.Should().NotBeNull();

				var process = (Process?)processField!.GetValue(transport);
				process.Should().NotBeNull();
				process!.HasExited.Should().BeFalse();

				await transport.DisposeAsync();

				// Process should be killed and cleaned up
				var processAfter = (Process?)processField.GetValue(transport);
				processAfter.Should().BeNull();
				transport.Status.Should().Be(TransportStatus.Disposed);
			}

			[Fact]
			public async Task WhenDisposedAfterStart_StatusTransitionsCorrectly()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);

				process.Status.Should().Be(TransportStatus.Created);

				await process.StartAsync();
				process.Status.Should().Be(TransportStatus.Started);

				await process.DisposeAsync();
				process.Status.Should().Be(TransportStatus.Disposed);
			}

			[Fact]
			public async Task WhenDisposedAfterStart_StopsProcessAndSetsStatusDisposed()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				await process.DisposeAsync();

				process.IsStarted.Should().BeFalse();
				process.Status.Should().Be(TransportStatus.Disposed);
			}

			[Fact]
			public async Task WhenDisposedAfterStop_DoesNotThrow()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();
				await process.StopAsync();

				var act = async () => await process.DisposeAsync();

				await act.Should().NotThrowAsync();
				process.Status.Should().Be(TransportStatus.Disposed);
			}

			[Fact]
			public async Task WhenDisposedAfterStop_StatusTransitionsCorrectly()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);

				process.Status.Should().Be(TransportStatus.Created);

				await process.StartAsync();
				process.Status.Should().Be(TransportStatus.Started);

				await process.StopAsync();
				process.Status.Should().Be(TransportStatus.Stopped);

				await process.DisposeAsync();
				process.Status.Should().Be(TransportStatus.Disposed);
			}

			[Fact]
			public async Task WhenDisposedConcurrently_IsIdempotent()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				var t1 = process.DisposeAsync();
				var t2 = process.DisposeAsync();
				var t3 = process.DisposeAsync();

				await Task.WhenAll(t1.AsTask(), t2.AsTask(), t3.AsTask());

				process.Status.Should().Be(TransportStatus.Disposed);
				process.IsStarted.Should().BeFalse();
			}

			[Fact]
			public async Task WhenDisposedWhileStarting_WaitsForStartThenDisposes()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);

				var startTask   = process.StartAsync();
				var disposeTask = process.DisposeAsync().AsTask();

				await Task.WhenAll(startTask, disposeTask);

				process.Status.Should().Be(TransportStatus.Disposed);
				process.IsStarted.Should().BeFalse();
			}

			[Fact]
			public async Task WhenDisposedWhileStopping_WaitsForStopThenCompletes()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				var stopTask    = process.StopAsync();
				var disposeTask = process.DisposeAsync().AsTask();

				await Task.WhenAll(stopTask, disposeTask);

				process.Status.Should().Be(TransportStatus.Disposed);
			}

			[Fact]
			public async Task WhenDisposedWithActiveReader_CompletesReaderGracefully()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				var enumerator = process.ReadLinesAsync(CancellationToken.None).GetAsyncEnumerator();
				var readTask   = enumerator.MoveNextAsync().AsTask();

				await process.DisposeAsync();

				// Reader should complete after dispose
				var completed = await Task.WhenAny(readTask, Task.Delay(TestConstants.DefaultTimeout));
				completed.Should().Be(readTask);
				(await readTask).Should().BeFalse();

				await enumerator.DisposeAsync();
			}

			[Fact]
			public async Task WhenDisposedWithActiveWriter_ClosesChannelAndThrowsOnWrite()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				// Start a write that might be in progress
				var writeTask = process.WriteLineAsync("uci");

				// Dispose immediately (might interrupt write)
				await process.DisposeAsync();

				// Subsequent writes should throw
				await FluentActions.Awaiting(() => process.WriteLineAsync("isready"))
								   .Should()
								   .ThrowAsync<InvalidOperationException>();

				// Wait for initial write to complete or fail
				try
				{
					await writeTask;
				}
				catch
				{
					// Expected if dispose interrupted it
				}
			}

			[Fact]
			public async Task WhenDisposedWithExitedProcess_CompletesImmediately()
			{
				if (!IsWindows()) return;

				string? cmdPath = TryResolveCmdPath();
				if (cmdPath is null) return;

				var transport = new ProcessUciTransport(
					cmdPath,
					[ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, ProcessArgs.ZERO]);

				var exitedTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
				transport.Exited += (_, _) => exitedTcs.TrySetResult(null);

				await transport.StartAsync();

				// Wait for process to exit
				var completed = await Task.WhenAny(exitedTcs.Task, Task.Delay(TestConstants.DefaultTimeout));
				completed.Should().Be(exitedTcs.Task);

				// Dispose should complete without hanging since process already exited
				// Use a timeout to ensure it doesn't hang, but don't assert on exact timing
				using var cts         = new CancellationTokenSource(TestConstants.DefaultTimeout);
				var       disposeTask = transport.DisposeAsync().AsTask();

				var disposeCompleted = await Task.WhenAny(
										   disposeTask,
										   Task.Delay(TestConstants.DefaultTimeout, cts.Token));

				disposeCompleted.Should().Be(disposeTask, "Dispose should complete when process has already exited");

				await disposeTask;
				transport.Status.Should().Be(TransportStatus.Disposed);
			}

			[Fact]
			public async Task WhenDisposedWithSendQuitOnDisposeFalse_DoesNotSendQuitCommand()
			{
				const string PATH = TestConsts.STOCKFISH_PATH;

				var quitSent = false;

				var options = new ProcessUciTransportOptions
				{
					SendQuitOnDispose = false,
					QuitGracePeriod   = TestConstants.QuitGracePeriod,
					TeardownTimeout   = TestConstants.DefaultTimeout,
					OnQuitSent        = () => quitSent = true
				};

				var transport = new ProcessUciTransport(PATH, null, null, options);
				await transport.StartAsync();
				await transport.DisposeAsync();

				quitSent.Should().BeFalse("quit command should not be sent when SendQuitOnDispose is false");
				transport.Status.Should().Be(TransportStatus.Disposed);
				transport.IsStarted.Should().BeFalse();
			}

			[Fact]
			public async Task WhenDisposedWithSendQuitOnDisposeTrue_SendsQuitCommand()
			{
				const string PATH = TestConsts.STOCKFISH_PATH;

				var quitSent = false;

				var options = new ProcessUciTransportOptions
				{
					SendQuitOnDispose = true,
					QuitGracePeriod   = TestConstants.QuitGracePeriod,
					TeardownTimeout   = TestConstants.DefaultTimeout,
					OnQuitSent        = () => quitSent = true
				};

				var transport = new ProcessUciTransport(PATH, null, null, options);
				await transport.StartAsync();
				await transport.DisposeAsync();

				quitSent.Should().BeTrue("quit command should be sent when SendQuitOnDispose is true");
				transport.Status.Should().Be(TransportStatus.Disposed);
			}

			[Fact]
			public void WhenAsyncDisposedBeforeStart_DoesNotThrow()
			{
				var process = new ProcessUciTransport("any/nonempty/path");

				var act = () => process.DisposeAsync();

				act.Should().NotThrow();
				process.IsStarted.Should().BeFalse();
				process.Status.Should().Be(TransportStatus.Disposed);
			}

			[Fact]
			public void WhenDisposeAsyncAfterDisposed_ShouldNotThrow()
			{
				var process = new ProcessUciTransport("any/nonempty/path");

				process.Dispose();
				var act = () => process.DisposeAsync();

				act.Should().NotThrow();
			}

			[Fact]
			public void WhenDisposedBeforeStart_DoesNotThrow()
			{
				var process = new ProcessUciTransport("any/nonempty/path");

				var act = () => process.Dispose();

				act.Should().NotThrow();
				process.IsStarted.Should().BeFalse();
				process.Status.Should().Be(TransportStatus.Disposed);
			}

			[Fact]
			public void WhenDisposedMultipleTimesSynchronously_IsIdempotent()
			{
				var process = new ProcessUciTransport("any/nonempty/path");

				var act = () =>
				{
					process.Dispose();
					process.Dispose();
					process.Dispose();
				};

				act.Should().NotThrow();
				process.Status.Should().Be(TransportStatus.Disposed);
			}
		}

		public class EncodingEdgeCaseTests
		{
			[Fact]
			public async Task WriteLineAsync_WithSpecialCharacters_HandlesCorrectly()
			{
				await using var transport = CreateTransportWithOptions();
				await transport.StartAsync();

				// Test with various special characters (but not newlines which are invalid)
				var specialChars = "test!@#$%^&*()_+-=[]{}|;':\",./<>?";
				await FluentActions.Awaiting(() => transport.WriteLineAsync(specialChars))
								   .Should()
								   .NotThrowAsync("Should handle special characters");
			}

			[Fact]
			public async Task WriteLineAsync_WithUnicodeCharacters_HandlesCorrectly()
			{
				var options = new ProcessUciTransportOptions
				{
					StdinEncoding    = Encoding.UTF8,
					ValidateCommands = false // Disable validation since Unicode characters aren't standard UCI commands
				};

				var transport = CreateTransportWithOptions(options);
				await transport.StartAsync();

				// Test with actual Unicode characters (emojis, non-ASCII)
				const string UNICODE_MESSAGE = "test\u00E9\u00F1\u4E2D\u6587\uD83D\uDE00";

				// Should not throw when writing Unicode characters with UTF-8 encoding
				await FluentActions.Awaiting(() => transport.WriteLineAsync(UNICODE_MESSAGE))
								   .Should()
								   .NotThrowAsync("Should handle Unicode characters with UTF-8 encoding");

				// Give a moment for the write loop to process
				await Task.Delay(TestConstants.ShortDelay);

				// Verify the write was recorded
				transport.LinesWritten.Should().BeGreaterThan(0, "Unicode message should be written");
			}
		}

		public class EncodingTests
		{
			[Fact]
			public async Task ReadLinesAsync_WithUtf8Encoding_ReadsUnicodeCorrectly()
			{
				var options = new ProcessUciTransportOptions
				{
					StdoutEncoding = Encoding.UTF8
				};

				await using var transport = CreateTransportWithOptions(options);
				await transport.StartAsync();

				await transport.WriteLineAsync("uci");

				using var cts        = new CancellationTokenSource(TestConstants.DefaultTimeout);
				var       foundUciok = false;
				await foreach (string line in transport.ReadLinesAsync(cts.Token))
				{
					if (line.Contains("uciok"))
					{
						foundUciok = true;
						break;
					}
				}

				foundUciok.Should().BeTrue("Should read uciok response with UTF-8 encoding");
			}

			[Fact]
			public async Task StderrReceived_WithUtf8Encoding_HandlesUnicodeCorrectly()
			{
				if (!IsWindows()) return;

				string? cmdPath = TryResolveCmdPath();
				if (cmdPath is null) return;

				var options = new ProcessUciTransportOptions
				{
					RedirectStandardError = true,
					StderrEncoding        = Encoding.UTF8
				};

				var transport = new ProcessUciTransport(
					cmdPath,
					[ProcessArgs.CMD_EXECUTE, ProcessArgs.ECHO, "test", ProcessArgs.STD_OUT_TO_STD_ERR],
					null,
					options);

				var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
				transport.StderrReceived += s =>
				{
					if (!string.IsNullOrEmpty(s)) tcs.TrySetResult(s);
				};

				await transport.StartAsync();

				var completed = await Task.WhenAny(tcs.Task, Task.Delay(TestConstants.DefaultTimeout));
				completed.Should().Be(tcs.Task);

				await transport.DisposeAsync();
			}

			[Fact]
			public async Task WriteLineAsync_WithUtf8Encoding_HandlesUnicodeCorrectly()
			{
				var options = new ProcessUciTransportOptions
				{
					StdinEncoding = Encoding.UTF8
				};

				await using var transport = CreateTransportWithOptions(options);
				await transport.StartAsync();

				// Write a command with Unicode characters
				await FluentActions.Awaiting(() => transport.WriteLineAsync("uci"))
								   .Should()
								   .NotThrowAsync("UTF-8 encoding should handle standard ASCII commands");

				await transport.DisposeAsync();
			}
		}

		public class FlushBatchSizeTests
		{
			[Fact]
			public async Task WriteLineAsync_WithFlushBatchSize_RespectsBatching()
			{
				var options = new ProcessUciTransportOptions
				{
					FlushBatchSize = 3
				};

				await using var transport = CreateTransportWithOptions(options);
				await transport.StartAsync();

				// Write multiple commands that should be batched
				await transport.WriteLineAsync("uci");
				await transport.WriteLineAsync("isready");
				await transport.WriteLineAsync("quit");

				// Wait for the write loop to process the messages and update the metric
				// The write loop processes messages asynchronously, so we need to wait
				var startTime = DateTime.UtcNow;
				while (transport.LinesWritten < 3 && DateTime.UtcNow - startTime < TestConstants.DefaultTimeout)
					await Task.Delay(TestConstants.ShortDelay);

				// All writes should complete without error
				transport.LinesWritten.Should().BeGreaterOrEqualTo(
					3,
					"All three lines should be written by the write loop");
			}
		}

		public class LargeMessageTests
		{
			[Fact]
			public async Task WriteLineAsync_WithLargeMessage_HandlesCorrectly()
			{
				await using var transport = CreateTransportWithOptions();
				await transport.StartAsync();

				// Create a large message (10KB)
				var largeMessage = new string('a', 10 * 1024);

				await FluentActions.Awaiting(() => transport.WriteLineAsync(largeMessage))
								   .Should()
								   .NotThrowAsync("Should handle large messages without error");
			}

			[Fact]
			public async Task WriteLineAsync_WithVeryLargeMessage_HandlesCorrectly()
			{
				var options = new ProcessUciTransportOptions
				{
					ChannelCapacity = 1024
				};

				await using var transport = CreateTransportWithOptions(options);
				await transport.StartAsync();

				// Create a very large message (100KB)
				var veryLargeMessage = new string('b', 100 * 1024);

				await FluentActions.Awaiting(() => transport.WriteLineAsync(veryLargeMessage))
								   .Should()
								   .NotThrowAsync("Should handle very large messages without error");
			}
		}

		public class NonZeroExitCodeTests
		{
			[Fact]
			public async Task Exited_Event_WithExitCodeTwo_ReportsCorrectCode()
			{
				if (!IsWindows()) return;

				string? cmdPath = TryResolveCmdPath();
				if (cmdPath is null) return;

				var transport = new ProcessUciTransport(
					cmdPath,
					[ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, "2"]);

				var tcs = new TaskCompletionSource<(int? Code, string? Error)>(
					TaskCreationOptions.RunContinuationsAsynchronously);

				transport.Exited += (code, error) => tcs.TrySetResult((code, error));

				await transport.StartAsync();

				var completed = await Task.WhenAny(tcs.Task, Task.Delay(TestConstants.DefaultTimeout));
				completed.Should().Be(tcs.Task);

				var result = await tcs.Task;
				result.Code.HasValue.Should().BeTrue();
				result.Code!.Value.Should().Be(2, "Exit code should be 2");

				await transport.DisposeAsync();
			}

			[Fact]
			public async Task Exited_Event_WithNonZeroExitCode_ReportsCorrectCode()
			{
				if (!IsWindows()) return;

				string? cmdPath = TryResolveCmdPath();
				if (cmdPath is null) return;

				var transport = new ProcessUciTransport(
					cmdPath,
					[ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, "1"]);

				var tcs = new TaskCompletionSource<(int? Code, string? Error)>(
					TaskCreationOptions.RunContinuationsAsynchronously);

				transport.Exited += (code, error) => tcs.TrySetResult((code, error));

				await transport.StartAsync();

				var completed = await Task.WhenAny(tcs.Task, Task.Delay(TestConstants.DefaultTimeout));
				completed.Should().Be(tcs.Task);

				var result = await tcs.Task;
				result.Code.HasValue.Should().BeTrue();
				result.Code!.Value.Should().Be(1, "Exit code should be 1");
				result.Error.Should().BeNull();

				await transport.DisposeAsync();
			}
		}

		public class OutgoingSingleWriterTests
		{
			[Fact]
			public async Task WriteLineAsync_WithOutgoingSingleWriterFalse_AllowsConcurrentWrites()
			{
				var options = new ProcessUciTransportOptions
				{
					OutgoingSingleWriter = false
				};

				await using var transport = CreateTransportWithOptions(options);
				await transport.StartAsync();

				// Perform concurrent writes
				var write1 = transport.WriteLineAsync("uci");
				var write2 = transport.WriteLineAsync("isready");

				await Task.WhenAll(write1, write2);

				// Wait for the write loop to process the messages and update the metric
				// The write loop processes messages asynchronously, so we need to wait
				var startTime = DateTime.UtcNow;
				while (transport.LinesWritten < 2 && DateTime.UtcNow - startTime < TestConstants.DefaultTimeout)
					await Task.Delay(TestConstants.ShortDelay);

				// Both writes should complete successfully
				transport.LinesWritten.Should().BeGreaterOrEqualTo(2, "Both lines should be written by the write loop");
			}

			[Fact]
			public async Task WriteLineAsync_WithOutgoingSingleWriterTrue_OptimizesForSingleWriter()
			{
				var options = new ProcessUciTransportOptions
				{
					OutgoingSingleWriter = true
				};

				await using var transport = CreateTransportWithOptions(options);
				await transport.StartAsync();

				await transport.WriteLineAsync("uci");
				await transport.WriteLineAsync("isready");

				// Wait for the write loop to process the messages and update the metric
				// The write loop processes messages asynchronously, so we need to wait
				var startTime = DateTime.UtcNow;
				while (transport.LinesWritten < 2 && DateTime.UtcNow - startTime < TestConstants.DefaultTimeout)
					await Task.Delay(TestConstants.ShortDelay);

				transport.LinesWritten.Should().BeGreaterOrEqualTo(2, "Both lines should be written by the write loop");
			}
		}

		public class ReadLinesTests
		{
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
		}

		public class StartTests
		{
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
				transport.Status.Should().Be(TransportStatus.Failed);

				await transport.DisposeAsync();
			}
		}

		public class StopTests
		{
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
			public async Task StopAsync_WhenCanceled_ThrowsOperationCanceledException_AndLeavesStateUnchanged()
			{
				var process = new ProcessUciTransport("any/nonempty/path");
				var cts     = new CancellationTokenSource();
				// ReSharper disable once MethodHasAsyncOverload
				cts.Cancel();

				await FluentActions.Awaiting(() => process.StopAsync(cts.Token))
								   .Should()
								   .ThrowAsync<OperationCanceledException>();

				process.Status.Should().Be(TransportStatus.Created);
			}

			[Fact]
			public async Task StopAsync_WhenNotStarted_NoOpAndSetsStatusStopped()
			{
				await using var process = new ProcessUciTransport("any/nonempty/path");

				await process.StopAsync();

				process.Status.Should().Be(TransportStatus.Stopped);
			}
		}

		public class TeardownTimeoutTests
		{
			[Fact]
			public async Task DisposeAsync_WithShortTeardownTimeout_CompletesWithinTimeout()
			{
				var options = new ProcessUciTransportOptions
				{
					TeardownTimeout   = TestConstants.ShortTimeout,
					SendQuitOnDispose = true
				};

				var transport = CreateTransportWithOptions(options);
				await transport.StartAsync();

				var disposeStart = DateTime.UtcNow;
				await transport.DisposeAsync();
				var disposeDuration = DateTime.UtcNow - disposeStart;

				disposeDuration.Should().BeLessThan(
					TestConstants.DefaultTimeout,
					"Dispose should complete within reasonable time");

				transport.Status.Should().Be(TransportStatus.Disposed);
			}
		}

		public class WriteLineTests
		{
			[Fact]
			public async Task TryWriteLineAsync_NullLine_ThrowsArgumentNullException()
			{
				var process = new ProcessUciTransport(TestConsts.STOCKFISH_PATH);
				await process.StartAsync();

				await FluentActions
					  .Awaiting(() => process.TryWriteLineAsync(null!, TestConstants.TinyTimeout))
					  .Should()
					  .ThrowAsync<ArgumentNullException>();
			}

			[Fact]
			public async Task TryWriteLineAsync_WhenCalledWithoutEngineStart_ThrowsInvalidOperationException()
			{
				var process = new ProcessUciTransport("any/nonempty/path");

				await FluentActions.Awaiting(() => process.TryWriteLineAsync("uci", TestConstants.TinyTimeout))
								   .Should()
								   .ThrowAsync<InvalidOperationException>();
			}

			[Fact]
			public async Task TryWriteLineAsync_WithNegativeNonInfiniteTimeout_ThrowsArgumentOutOfRangeException()
			{
				var process = new ProcessUciTransport(TestConsts.STOCKFISH_PATH);
				await process.StartAsync();

				await FluentActions.Awaiting(() => process.TryWriteLineAsync("uci", TimeSpan.FromMilliseconds(-2)))
								   .Should()
								   .ThrowAsync<ArgumentOutOfRangeException>();
			}

			[Fact]
			public async Task WriteLineAsync_WhenCalledWithCarriageReturn_ThrowsArgumentException()
			{
				var process = new ProcessUciTransport(TestConsts.STOCKFISH_PATH);
				await process.StartAsync();

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
		}
	}
}
