using System.Text;
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
	private static ProcessUciTransport CreateTransportWithOptions(ProcessUciTransportOptions options) =>
		new(TestConsts.STOCKFISH_PATH, null, null, options);

	private static string TryResolveCmdPath()
	{
		string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
		if (!string.IsNullOrWhiteSpace(systemRoot))
		{
			string cmd = Path.Combine(systemRoot, "System32", "cmd.exe");
			if (File.Exists(cmd)) return cmd;
		}

		string  envCmd = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\cmd.exe");
		string? file   = File.Exists(envCmd) ? envCmd : null;
		if (file is null)
			throw new InvalidOperationException("Unable to resolve cmd.exe path");

		return file;
	}

	public abstract class IntegrationTests
	{
		public class DisposeTests
		{
			[Fact]
			public async Task DisposeAsync_WhenCalledMultipleTimes_ShouldBeIdempotent()
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
			public async Task DisposeAsync_WhenDisposedAfterStart_ShouldStopProcessAndResetIsStarted()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);

				await process.StartAsync();
				await process.DisposeAsync();

				process.IsStarted.Should().BeFalse();
			}
		}

		public class ExitedTests
		{
			[Fact]
			public async Task Exited_WhenExited_ShouldBeRaisedOnlyOnce()
			{
				var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH);

				var count = 0;
				var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
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
			public async Task Exited_WhenHandlerThrows_ShouldRaiseErrorEventAndNotCrash()
			{
				var transport = new ProcessUciTransport(
					TestConsts.STOCKFISH_PATH,
					[ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, ProcessArgs.ZERO]);

				var errorTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
				transport.Error += ex => errorTcs.TrySetResult(ex);

				transport.Exited += (_, _) => throw new InvalidOperationException("boom from handler");

				await transport.StartAsync();

				var completed = await Task.WhenAny(errorTcs.Task, Task.Delay(TestConstants.DefaultTimeout));
				completed.Should().Be(errorTcs.Task, "Error event should be raised when Exited handler throws");

				(await errorTcs.Task).Should().BeOfType<InvalidOperationException>();

				await transport.DisposeAsync();
			}

			[Fact]
			public async Task Exited_WhenMultipleReaders_ShouldRaiseExitedOnce()
			{
				string cmdPath = TryResolveCmdPath();


				var options = new ProcessUciTransportOptions { SingleReader = false };
				var transport = new ProcessUciTransport(
					cmdPath,
					[ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, ProcessArgs.ZERO],
					null,
					options);

				var exitedCount = 0;
				var exitedTcs   = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

				transport.Exited += (_, _) =>
				{
					Interlocked.Increment(ref exitedCount);
					exitedTcs.TrySetResult(null);
				};

				await transport.StartAsync();

				var e1 = transport.ReadLinesAsync(CancellationToken.None).GetAsyncEnumerator();
				var e2 = transport.ReadLinesAsync(CancellationToken.None).GetAsyncEnumerator();

				var completed = await Task.WhenAny(exitedTcs.Task, Task.Delay(TestConstants.DefaultTimeout));
				completed.Should().Be(exitedTcs.Task, "Process should exit");

				await Task.Delay(TestConstants.StandardDelay);

				exitedCount.Should().Be(1, "Exited event should be raised exactly once even with multiple readers");

				await e1.DisposeAsync();
				await e2.DisposeAsync();
				await transport.DisposeAsync();
			}

			[Fact]
			public async Task Exited_WhenProcessExits_ShouldRaiseEventWithExitCode()
			{
				string cmdPath = TryResolveCmdPath();


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

			[Fact]
			public async Task Exited_WhenProcessExits_ShouldRaiseExitedEvent()
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
			public async Task Exited_WhenProcessExitsDuringRead_ShouldCompleteReadGracefully()
			{
				string cmdPath = TryResolveCmdPath();


				var transport = new ProcessUciTransport(
					cmdPath,
					[ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, ProcessArgs.ZERO]);

				var exitedTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
				transport.Exited += (_, _) => exitedTcs.TrySetResult(null);

				await transport.StartAsync();

				var enumerator = transport.ReadLinesAsync(CancellationToken.None).GetAsyncEnumerator();
				var readTask   = enumerator.MoveNextAsync().AsTask();

				var completed = await Task.WhenAny(exitedTcs.Task, Task.Delay(TestConstants.DefaultTimeout));
				completed.Should().Be(exitedTcs.Task, "Process should exit");

				var readCompleted = await Task.WhenAny(readTask, Task.Delay(TestConstants.DefaultTimeout));
				readCompleted.Should().Be(readTask, "Read should complete when process exits");

				(await readTask).Should().BeFalse("No more lines should be available after process exits");

				await enumerator.DisposeAsync();
				await transport.DisposeAsync();
			}

			[Fact]
			public async Task Exited_WhenProcessExitsDuringWrite_ShouldRaiseExitedEventAndWriteShouldFail()
			{
				string cmdPath = TryResolveCmdPath();


				var transport = new ProcessUciTransport(
					cmdPath,
					[ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, ProcessArgs.ZERO]);

				var exitedTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
				transport.Exited += (_, _) => exitedTcs.TrySetResult(null);

				await transport.StartAsync();

				// Wait for process to exit
				var completed = await Task.WhenAny(exitedTcs.Task, Task.Delay(TestConstants.DefaultTimeout));
				completed.Should().Be(exitedTcs.Task, "Process should exit");

				// Wait for exit notification to update state (MarkProcessAlive(false))
				await Task.Delay(TestConstants.StandardDelay);

				// Verify process has exited
				transport.IsHealthy.Should().BeFalse("IsHealthy should be false after process exits");

				// Attempting to write after process has exited should throw
				await FluentActions.Awaiting(() => transport.WriteLineAsync("uci"))
								   .Should()
								   .ThrowAsync<InvalidOperationException>()
								   .WithMessage("*Engine process has exited*");

				await transport.DisposeAsync();
			}
		}

		public class IsHealthyTests
		{
			[Fact]
			public async Task IsHealthy_WhenDisposed_ShouldBeFalse()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				await process.DisposeAsync();
				process.IsHealthy.Should().BeFalse();
			}

			[Fact]
			public async Task IsHealthy_WhenProcessExitsUnexpectedly_ShouldReturnFalse()
			{
				string cmdPath = TryResolveCmdPath();


				var transport = new ProcessUciTransport(
					cmdPath,
					[ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, ProcessArgs.ZERO]);

				await transport.StartAsync();

				transport.IsHealthy.Should().BeTrue("Process should be healthy immediately after start");

				var exitedTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
				transport.Exited += (_, _) => exitedTcs.TrySetResult(null);

				var completed = await Task.WhenAny(exitedTcs.Task, Task.Delay(TestConstants.DefaultTimeout));
				completed.Should().Be(exitedTcs.Task, "Process should exit");

				await Task.Delay(TestConstants.ShortDelay);

				transport.IsHealthy.Should().BeFalse("IsHealthy should be false after process exits unexpectedly");
				transport.IsStarted.Should().BeTrue("IsStarted should remain true even after process exits");

				await transport.DisposeAsync();
			}

			[Fact]
			public async Task IsHealthy_WhenRunning_ShouldBeTrue()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				process.IsHealthy.Should().BeTrue();

				await process.DisposeAsync();
			}

			[Fact]
			public async Task IsHealthy_WhenStopped_ShouldBeFalse()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				await process.StopAsync();
				process.IsHealthy.Should().BeFalse();

				await process.DisposeAsync();
			}
		}

		public class ReadLinesTests
		{
			[Fact]
			public async Task ReadLinesAsync_WhenConcurrentWithStopAsync_ShouldCompleteGracefully()
			{
				const string PATH      = TestConsts.STOCKFISH_PATH;
				var          transport = new ProcessUciTransport(PATH);
				await transport.StartAsync();

				var enumerator = transport.ReadLinesAsync(CancellationToken.None).GetAsyncEnumerator();
				var readTask   = enumerator.MoveNextAsync().AsTask();

				var stopTask = transport.StopAsync();

				var completed = await Task.WhenAny(readTask, stopTask, Task.Delay(TestConstants.DefaultTimeout));
				completed.Should().NotBe(Task.Delay(TestConstants.DefaultTimeout), "Operations should complete");

				await stopTask;

				var readCompleted = await Task.WhenAny(readTask, Task.Delay(TestConstants.DefaultTimeout));
				readCompleted.Should().Be(readTask, "Read should complete after stop");

				(await readTask).Should().BeFalse("No more lines should be available after stop");

				await enumerator.DisposeAsync();
				await transport.DisposeAsync();
			}

			[Fact]
			public async Task ReadLinesAsync_WhenDisposed_ShouldCompleteGracefully()
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
			public async Task ReadLinesAsync_WhenStopped_ShouldCompleteGracefully()
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
			public async Task ReadLinesAsync_WithSecondConcurrentReader_ShouldThrowInvalidOperationException()
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
			public async Task ReadLinesAsync_WithSingleReader_WhenFirstDisposed_ReleasesGateForSecondReader()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				using var cts1          = new CancellationTokenSource(TestConstants.TinyTimeout);
				var       e1            = process.ReadLinesAsync(cts1.Token).GetAsyncEnumerator(cts1.Token);
				var       firstMoveTask = e1.MoveNextAsync().AsTask();

				try
				{
					await firstMoveTask;
				}
				catch { }

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

		public class ReadLoopTests
		{
			[Fact]
			public async Task ReadLoop_WhenChannelFull_ShouldIncrementBackpressureEvents()
			{
				string cmdPath = TryResolveCmdPath();


				var options = new ProcessUciTransportOptions
				{
					ChannelCapacity = 1
				};

				var transport = new ProcessUciTransport(
					cmdPath,
					[ProcessArgs.CMD_EXECUTE, ProcessArgs.ECHO, "L1", ProcessArgs.CHAIN, ProcessArgs.ECHO, "L2"],
					null,
					options);

				await transport.StartAsync();

				await Task.Delay(TestConstants.LongerDelay);

				await transport.StopAsync();

				transport.BackpressureEvents.Should().BeGreaterThan(0);

				await transport.DisposeAsync();
			}

			[Fact]
			public async Task ReadLoop_WhenEmptyLines_ShouldSkipEmptyLinesAndEmitNonEmpty()
			{
				string cmdPath = TryResolveCmdPath();


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
		}

		public class StartTests
		{
			[Fact]
			public async Task StartAsync_WhenAlreadyStarted_ShouldBeIdempotent()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				var before = process.Status;

				await process.StartAsync();
				var after = process.Status;

				process.IsStarted.Should().BeTrue();
				before.Should().Be(TransportStatus.Started);
				after.Should().Be(TransportStatus.Started);

				await process.DisposeAsync();
			}

			[Fact]
			public async Task StartAsync_WhenCalledWithValidProcess_ShouldStartProcess()
			{
				const string    PATH    = TestConsts.STOCKFISH_PATH;
				await using var process = new ProcessUciTransport(PATH);

				await process.StartAsync();

				process.IsStarted.Should().BeTrue();
			}

			[Fact]
			public async Task StartAsync_WhenConcurrentCalls_ShouldStartOnceAndCompleteAll()
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
			public async Task StartAsync_WhenMultipleConcurrent_ShouldStartOnceAndAwaitOthers()
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
			public async Task StartAsync_WhenStopping_ShouldThrowInvalidOperationException()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				var stoppingTask = process.StopAsync();

				bool conditionMet = await AsyncTestHelpers.WaitForConditionAsync(
										() => process.Status == TransportStatus.Stopping,
										TestConstants.ShortDelay,
										TestConstants.DefaultTimeout);

				conditionMet.Should().BeTrue("Status should transition to Stopping");

				await FluentActions.Awaiting(() => process.StartAsync())
								   .Should()
								   .ThrowAsync<InvalidOperationException>();

				await stoppingTask;
				await process.DisposeAsync();
			}

			[Fact]
			public async Task StartAsync_WithInvalidWorkingDirectory_ShouldThrowArgumentException()
			{
				const string PATH       = TestConsts.STOCKFISH_PATH;
				string       invalidDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
				var          process    = new ProcessUciTransport(PATH, null, invalidDir);

				await FluentActions.Awaiting(() => process.StartAsync())
								   .Should()
								   .ThrowAsync<ArgumentException>();

				await process.DisposeAsync();
			}
		}

		public class StdErrReceivedTests
		{
			[Fact]
			public async Task StderrReceived_WhenHandlerThrows_ShouldSwallowExceptionAndNotCrash()
			{
				string cmdPath = TryResolveCmdPath();


				var options = new ProcessUciTransportOptions { RedirectStandardError = true };
				var transport = new ProcessUciTransport(
					cmdPath,
					[ProcessArgs.CMD_EXECUTE, ProcessArgs.ECHO, "oops", ProcessArgs.STD_OUT_TO_STD_ERR],
					null,
					options);

				var errorTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
				transport.Error += ex => errorTcs.TrySetResult(ex);

				transport.StderrReceived += _ => throw new InvalidOperationException("stderr handler boom");

				await transport.StartAsync();

				var completed = await Task.WhenAny(errorTcs.Task, Task.Delay(TestConstants.BackpressureDelay));
				completed.Should().NotBe(
					errorTcs.Task,
					"stderr handler exceptions must be swallowed and not surface via Error");

				await transport.DisposeAsync();
			}

			[Fact]
			public async Task StderrReceived_WhenRedirected_ShouldFireEvent()
			{
				string cmdPath = TryResolveCmdPath();


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
			public async Task StderrReceived_WhenStderrDisabled_ShouldNotStartStderrLoop()
			{
				string cmdPath = TryResolveCmdPath();


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

				var completed = await Task.WhenAny(tcs.Task, Task.Delay(TestConstants.BackpressureDelay));
				completed.Should().NotBe(tcs.Task);

				await transport.DisposeAsync();
			}
		}

		public class StopTests
		{
			[Fact]
			public async Task StopAsync_WhenCalled_ShouldDisposeStreamsAndCompleteChannels()
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
			public async Task StopAsync_WhenSendQuitOnStop_ShouldSendQuitCommandAndThenKillAfterGrace()
			{
				const string PATH = TestConsts.STOCKFISH_PATH;

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

				quitSent.Should().BeTrue("quit command should be sent when SendQuitOnStop is true");
				transport.Status.Should().Be(TransportStatus.Stopped);

				await transport.DisposeAsync();
			}
		}

		public class TryWriteLineTests
		{
			[Fact]
			public async Task TryWriteLineAsync_WhenCanceledDuringWait_ShouldThrowOperationCanceledException()
			{
				var options = new ProcessUciTransportOptions
				{
					ChannelCapacity  = 1,
					DisableWriteLoop = true,
					ValidateCommands = true
				};

				var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH, null, null, options);
				await transport.StartAsync();

				bool ok1 = await transport.TryWriteLineAsync("uci", TestConstants.SmallTimeout);
				ok1.Should().BeTrue();

				var cts = new CancellationTokenSource(TestConstants.MediumDelay);
				await FluentActions
					  .Awaiting(() => transport.TryWriteLineAsync("isready", TestConstants.DefaultTimeout, cts.Token))
					  .Should()
					  .ThrowAsync<OperationCanceledException>();

				await transport.DisposeAsync();
			}

			[Fact]
			public async Task TryWriteLineAsync_WhenChannelClosedDuringStop_ShouldThrowInvalidOperationException()
			{
				var options = new ProcessUciTransportOptions
				{
					ChannelCapacity  = 1,
					DisableWriteLoop = true
				};

				var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH, null, null, options);

				await transport.StartAsync();

				bool ok1 = await transport.TryWriteLineAsync("uci", TestConstants.TinyTimeout);
				ok1.Should().BeTrue();

				var writeTask = transport.TryWriteLineAsync("isready", TestConstants.DefaultTimeout);

				await transport.Awaiting(_ => transport.StopAsync()).Should().NotThrowAsync();

				await FluentActions.Awaiting(async () => await writeTask)
								   .Should()
								   .ThrowAsync<InvalidOperationException>()
								   .WithMessage("Transport is stopping or stopped; cannot write.");
			}

			[Fact]
			public async Task TryWriteLineAsync_WhenChannelFullAndTinyTimeout_ShouldSpinAndReturnFalse()
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
			public async Task TryWriteLineAsync_WhenChannelFullAndZeroTimeout_ShouldReturnFalse()
			{
				var options = new ProcessUciTransportOptions
				{
					ChannelCapacity  = 1,
					DisableWriteLoop = true,
					ValidateCommands = true
				};

				var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH, null, null, options);
				await transport.StartAsync();

				bool ok1 = await transport.TryWriteLineAsync("uci", TestConstants.TinyTimeout);
				ok1.Should().BeTrue();

				bool ok2 = await transport.TryWriteLineAsync("isready", TimeSpan.Zero);
				ok2.Should().BeFalse();

				await transport.DisposeAsync();
			}

			[Fact]
			public async Task TryWriteLineAsync_WhenChannelReady_ShouldReturnTrue()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				bool ok = await process.TryWriteLineAsync("uci", TestConstants.ShortTimeout);
				ok.Should().BeTrue();

				await process.DisposeAsync();
			}

			[Fact]
			public async Task TryWriteLineAsync_WhenDisposed_ShouldThrowInvalidOperationException()
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
			public async Task TryWriteLineAsync_WhenInfiniteTimeoutAndChannelFull_ShouldComplete()
			{
				var options = new ProcessUciTransportOptions
				{
					ChannelCapacity  = 1,
					DisableWriteLoop = false
				};

				var transport = new ProcessUciTransport(TestConsts.STOCKFISH_PATH, null, null, options);

				await transport.StartAsync();

				bool ok1 = await transport.TryWriteLineAsync("uci", TestConstants.TinyTimeout);
				ok1.Should().BeTrue();

				bool ok2 = await transport.TryWriteLineAsync("isready", Timeout.InfiniteTimeSpan);
				ok2.Should().BeTrue();

				await transport.DisposeAsync();
			}

			[Fact]
			public async Task TryWriteLineAsync_WhenProcessHasExited_ShouldThrowInvalidOperationException()
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
			public async Task TryWriteLineAsync_WhenStopped_ShouldThrowInvalidOperationException()
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
			public async Task TryWriteLineAsync_WhenTimeout_ShouldReturnFalseAfterTimeout()
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
			public async Task WriteLineAsync_WhenCalledWithNewline_ShouldThrowArgumentException()
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
			public async Task WriteLineAsync_WhenCanceled_ShouldThrowOperationCanceledException()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				var cts = new CancellationTokenSource();
				cts.Cancel();

				await FluentActions.Awaiting(() => process.WriteLineAsync("uci", cts.Token))
								   .Should()
								   .ThrowAsync<OperationCanceledException>();

				await process.DisposeAsync();
			}

			[Fact]
			public async Task WriteLineAsync_WhenDisposed_ShouldThrowInvalidOperationException()
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
			public async Task WriteLineAsync_WhenEngineStarted_ShouldWriteLineToEngine()
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
			public async Task WriteLineAsync_WhenNull_ShouldThrowArgumentNullException()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				await FluentActions
					  .Awaiting(() => process.WriteLineAsync(null!))
					  .Should()
					  .ThrowAsync<ArgumentNullException>();

				await process.DisposeAsync();
			}

			[Fact]
			public async Task WriteLineAsync_WhenProcessHasExited_ShouldThrowInvalidOperationException()
			{
				var transport = new ProcessUciTransport(
					TestConsts.STOCKFISH_PATH,
					[ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, ProcessArgs.ZERO]);

				var exitedTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
				transport.Exited += (_, _) => exitedTcs.TrySetResult(null);

				await transport.StartAsync();

				var completed = await Task.WhenAny(exitedTcs.Task, Task.Delay(TestConstants.DefaultTimeout));
				completed.Should().Be(exitedTcs.Task);

				await FluentActions.Awaiting(() => transport.WriteLineAsync("uci"))
								   .Should()
								   .ThrowAsync<InvalidOperationException>();

				await transport.DisposeAsync();
			}

			[Fact]
			public async Task WriteLineAsync_WhenStopped_ShouldThrowInvalidOperationException()
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
			public async Task WriteLineAsync_WhenUnknownCommand_ShouldNotThrowAndReadingContinues()
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
			public async Task WriteLineAsync_WhenValidationDisabled_ShouldAllowWhitespaceAndNewline()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          options = new ProcessUciTransportOptions { ValidateCommands = false };
				var          process = new ProcessUciTransport(PATH, null, null, options);
				await process.StartAsync();

				await FluentActions.Awaiting(() => process.WriteLineAsync("   "))
								   .Should()
								   .NotThrowAsync();

				await FluentActions.Awaiting(() => process.WriteLineAsync("uci\n"))
								   .Should()
								   .NotThrowAsync();

				await process.DisposeAsync();
			}

			[Fact]
			public async Task WriteLineAsync_WhenWhitespace_ShouldThrowArgumentException()
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
		}
	}

	public static class UnitTests
	{
		public class ConstructorTests
		{
			[Fact]
			public void Constructor_WhenArgsContainsNull_ShouldThrowArgumentException()
			{
				string[] argsWithNull = ["arg1", null!, "arg2"];

				var act = () => _ = new ProcessUciTransport("any/nonempty/path", argsWithNull);

				act.Should().Throw<ArgumentException>()
				   .WithMessage("*cannot contain null values*");
			}

			[Fact]
			public void Constructor_WhenDefault_ShouldHaveInitialStateCorrect()
			{
				var process = new ProcessUciTransport("any/nonempty/path");

				process.Status.Should().Be(TransportStatus.Created);
				process.IsStarted.Should().BeFalse();
				process.BackpressureEvents.Should().Be(0);
				process.LinesRead.Should().Be(0);
				process.LinesWritten.Should().Be(0);
			}

			[Fact]
			public void Constructor_WhenEmptyPath_ShouldThrowArgumentException()
			{
				var act = () => _ = new ProcessUciTransport("");

				act.Should().Throw<ArgumentException>();
			}

			[Fact]
			public void Constructor_WhenFourParameterOverload_ShouldWorkCorrectly()
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
			public void Constructor_WhenInvalidChannelCapacity_ShouldThrowArgumentOutOfRangeException()
			{
				var    invalidOptions = new ProcessUciTransportOptions { ChannelCapacity = 0 };
				Action act = () => _ = new ProcessUciTransport("any/nonempty/path", null, null, invalidOptions);

				act.Should().Throw<ArgumentOutOfRangeException>();
			}

			[Fact]
			public void Constructor_WhenInvalidNewLine_ShouldThrowArgumentException()
			{
				var    invalidOptions = new ProcessUciTransportOptions { NewLine = "" };
				Action act = () => _ = new ProcessUciTransport("any/nonempty/path", null, null, invalidOptions);

				act.Should().Throw<ArgumentException>();
			}

			[Fact]
			public void Constructor_WhenNullPath_ShouldThrowArgumentException()
			{
				var act = () => _ = new ProcessUciTransport(null!);

				act.Should().Throw<ArgumentException>();
			}

			[Fact]
			public void Constructor_WhenWhitespacePath_ShouldThrowArgumentException()
			{
				var act = () => _ = new ProcessUciTransport("   ");

				act.Should().Throw<ArgumentException>();
			}
		}

		public class DisposeAsyncTests
		{
			[Fact]
			public async Task DisposeAsync_AfterStop_ShouldNotThrow()
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
			public async Task DisposeAsync_WhenCalledConcurrently_ShouldBeIdempotent()
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
			public async Task DisposeAsync_WhenDisposed_IsHealthyShouldReturnFalse()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				process.IsHealthy.Should().BeTrue();

				await process.DisposeAsync();

				process.IsHealthy.Should().BeFalse();
			}

			[Fact]
			public async Task DisposeAsync_WhenNormalDisposal_ErrorEventShouldNotBeRaised()
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
			public async Task DisposeAsync_WhenSendQuitOnDisposeFalse_ShouldNotSendQuitCommand()
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
			public async Task DisposeAsync_WhenSendQuitOnDisposeTrue_ShouldSendQuitCommand()
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
			public async Task DisposeAsync_WhileStarting_ShouldWaitForStartThenDispose()
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
			public async Task DisposeAsync_WhileStopping_ShouldWaitForStopThenComplete()
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
			public async Task DisposeAsync_WithActiveReader_ShouldCompleteReaderGracefully()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				var enumerator = process.ReadLinesAsync(CancellationToken.None).GetAsyncEnumerator();
				var readTask   = enumerator.MoveNextAsync().AsTask();

				await process.DisposeAsync();

				var completed = await Task.WhenAny(readTask, Task.Delay(TestConstants.DefaultTimeout));
				completed.Should().Be(readTask);
				(await readTask).Should().BeFalse();

				await enumerator.DisposeAsync();
			}

			[Fact]
			public async Task DisposeAsync_WithActiveWriter_ShouldCloseChannelAndThrowOnWrite()
			{
				const string PATH    = TestConsts.STOCKFISH_PATH;
				var          process = new ProcessUciTransport(PATH);
				await process.StartAsync();

				var writeTask = process.WriteLineAsync("uci");

				await process.DisposeAsync();

				await FluentActions.Awaiting(() => process.WriteLineAsync("isready"))
								   .Should()
								   .ThrowAsync<InvalidOperationException>();

				try
				{
					await writeTask;
				}
				catch { }
			}

			[Fact]
			public async Task DisposeAsync_WithExitedProcess_ShouldCompleteImmediately()
			{
				string cmdPath = TryResolveCmdPath();


				var transport = new ProcessUciTransport(
					cmdPath,
					[ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, ProcessArgs.ZERO]);

				var exitedTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
				transport.Exited += (_, _) => exitedTcs.TrySetResult(null);

				await transport.StartAsync();

				var completed = await Task.WhenAny(exitedTcs.Task, Task.Delay(TestConstants.DefaultTimeout));
				completed.Should().Be(exitedTcs.Task);

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
			public async Task DisposeAsync_WithShortTeardownTimeout_ShouldCompleteWithinTimeout()
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

		public class ReadLinesAsyncTests
		{
			[Fact]
			public async Task ReadLinesAsync_WithUtf8Encoding_ShouldReadUnicodeCorrectly()
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
		}

		public class StartAsyncTests
		{
			[Fact]
			public async Task StartAsync_WhenCanceled_ShouldThrowAndCleanState()
			{
				var process = new ProcessUciTransport("any/nonempty/path");
				var cts     = new CancellationTokenSource();
				cts.Cancel();

				await FluentActions.Awaiting(() => process.StartAsync(cts.Token))
								   .Should()
								   .ThrowAsync<OperationCanceledException>();

				process.IsStarted.Should().BeFalse();
			}

			[Fact]
			public async Task StartAsync_WhenConcurrentCallsAndOneFails_ShouldHandleGracefully()
			{
				string missing = Path.Combine(
					Path.GetTempPath(),
					"no-such-dir",
					Guid.NewGuid().ToString("N"),
					"missing.exe");

				var transport = new ProcessUciTransport(missing);

				var start1 = transport.StartAsync();
				var start2 = transport.StartAsync();
				var start3 = transport.StartAsync();

				string[] allTasks = await Task.WhenAll(
										start1.ContinueWith(t => t.Exception != null ? "failed" : "success"),
										start2.ContinueWith(t => t.Exception != null ? "failed" : "success"),
										start3.ContinueWith(t => t.Exception != null ? "failed" : "success"));

				allTasks.Should().AllBe("failed", "All concurrent StartAsync calls should fail when path is invalid");
				transport.IsStarted.Should().BeFalse();
				transport.Status.Should().Be(TransportStatus.Failed);

				await transport.DisposeAsync();
			}

			[Fact]
			public async Task StartAsync_WhenConcurrentWithStopAsync_ShouldHandleGracefully()
			{
				const string PATH      = TestConsts.STOCKFISH_PATH;
				var          transport = new ProcessUciTransport(PATH);

				var startTask = transport.StartAsync();
				await Task.Delay(TestConstants.ShortDelay);

				var stopTask = transport.StopAsync();

				await Task.WhenAll(startTask, stopTask);

				transport.Status.Should().Be(TransportStatus.Stopped);
				transport.IsStarted.Should().BeFalse();

				await transport.DisposeAsync();
			}

			[Fact]
			public async Task StartAsync_WhenDisposed_ShouldThrowObjectDisposedException()
			{
				var process = new ProcessUciTransport("any/nonempty/path");
				await process.DisposeAsync();

				await FluentActions.Awaiting(() => process.StartAsync())
								   .Should()
								   .ThrowAsync<ObjectDisposedException>();
			}

			[Fact]
			public async Task StartAsync_WhenFailsEarly_ShouldSetFailedStatusAndNotRaiseErrorEvent()
			{
				string missing = Path.Combine(
					Path.GetTempPath(),
					"no-such-dir",
					Guid.NewGuid().ToString("N"),
					"missing.exe");

				var transport = new ProcessUciTransport(missing);

				var errors = new List<Exception>();
				transport.Error += ex => errors.Add(ex);

				try
				{
					await transport.StartAsync();
				}
				catch (ArgumentException)
				{
					// Expected to throw
				}

				await Task.Delay(TestConstants.StandardDelay);

				errors.Should().BeEmpty(
					"Error events should not be raised when start fails early (before initialization)");

				transport.Status.Should().Be(TransportStatus.Failed, "Status should be set to Failed when start fails");
				transport.IsStarted.Should().BeFalse("Transport should not be started after failure");

				await transport.DisposeAsync();
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

		public class StderrReceivedTests
		{
			[Fact]
			public async Task StderrReceived_WithUtf8Encoding_ShouldHandleUnicodeCorrectly()
			{
				string cmdPath = TryResolveCmdPath();


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
		}

		public class StopAsyncTests
		{
			[Fact]
			public async Task StopAsync_WhenCanceled_ShouldThrowOperationCanceledExceptionAndLeaveStateUnchanged()
			{
				var process = new ProcessUciTransport("any/nonempty/path");
				var cts     = new CancellationTokenSource();
				cts.Cancel();

				await FluentActions.Awaiting(() => process.StopAsync(cts.Token))
								   .Should()
								   .ThrowAsync<OperationCanceledException>();

				process.Status.Should().Be(TransportStatus.Created);
			}

			[Fact]
			public async Task StopAsync_WhenConcurrentCalls_ShouldCompleteAll()
			{
				const string PATH      = TestConsts.STOCKFISH_PATH;
				var          transport = new ProcessUciTransport(PATH);
				await transport.StartAsync();

				var stop1 = transport.StopAsync();
				var stop2 = transport.StopAsync();
				var stop3 = transport.StopAsync();

				await Task.WhenAll(stop1, stop2, stop3);

				transport.Status.Should().Be(TransportStatus.Stopped);
				transport.IsStarted.Should().BeFalse();

				await transport.DisposeAsync();
			}

			[Fact]
			public async Task StopAsync_WhenDisposed_ShouldThrowObjectDisposedException()
			{
				var process = new ProcessUciTransport("any/nonempty/path");
				await process.DisposeAsync();

				await FluentActions.Awaiting(() => process.StopAsync())
								   .Should()
								   .ThrowAsync<ObjectDisposedException>();
			}
		}

		public class TryWriteLineAsyncTests
		{
			[Fact]
			public async Task TryWriteLineAsync_WhenNegativeNonInfiniteTimeout_ShouldThrowArgumentOutOfRangeException()
			{
				var process = new ProcessUciTransport(TestConsts.STOCKFISH_PATH);
				await process.StartAsync();

				await FluentActions.Awaiting(() => process.TryWriteLineAsync("uci", TimeSpan.FromMilliseconds(-2)))
								   .Should()
								   .ThrowAsync<ArgumentOutOfRangeException>();
			}

			[Fact]
			public async Task TryWriteLineAsync_WhenNotStarted_ShouldThrowInvalidOperationException()
			{
				var process = new ProcessUciTransport("any/nonempty/path");

				await FluentActions.Awaiting(() => process.TryWriteLineAsync("uci", TestConstants.TinyTimeout))
								   .Should()
								   .ThrowAsync<InvalidOperationException>();
			}

			[Fact]
			public async Task TryWriteLineAsync_WhenNullLine_ShouldThrowArgumentNullException()
			{
				var process = new ProcessUciTransport(TestConsts.STOCKFISH_PATH);
				await process.StartAsync();

				await FluentActions
					  .Awaiting(() => process.TryWriteLineAsync(null!, TestConstants.TinyTimeout))
					  .Should()
					  .ThrowAsync<ArgumentNullException>();
			}

			[Fact]
			public async Task TryWriteLineAsync_WithMessageExceedingMaxLength_ShouldThrowArgumentException()
			{
				var options = new ProcessUciTransportOptions
				{
					MaxLineLength = 2048
				};

				await using var transport = CreateTransportWithOptions(options);
				await transport.StartAsync();

				var tooLargeMessage = new string('z', 2049);

				await FluentActions
					  .Awaiting(() => transport.TryWriteLineAsync(tooLargeMessage, TimeSpan.FromSeconds(1)))
					  .Should()
					  .ThrowAsync<ArgumentException>()
					  .WithMessage("*exceeds maximum length of 2048*");
			}
		}

		public class WriteLineAsyncTests
		{
			[Fact]
			public async Task WriteLineAsync_WhenNotStarted_ShouldThrowInvalidOperationException()
			{
				var process = new ProcessUciTransport("any/nonempty/path");

				await FluentActions
					  .Awaiting(() => process.WriteLineAsync("uci"))
					  .Should()
					  .ThrowAsync<InvalidOperationException>();
			}

			[Fact]
			public async Task WriteLineAsync_WithCarriageReturn_ShouldThrowArgumentException()
			{
				var process = new ProcessUciTransport(TestConsts.STOCKFISH_PATH);
				await process.StartAsync();

				await FluentActions.Awaiting(() => process.WriteLineAsync("uci\r"))
								   .Should()
								   .ThrowAsync<ArgumentException>();
			}

			[Fact]
			public async Task WriteLineAsync_WithFlushBatchSize_ShouldRespectBatching()
			{
				var options = new ProcessUciTransportOptions
				{
					FlushBatchSize = 3
				};

				await using var transport = CreateTransportWithOptions(options);
				await transport.StartAsync();

				await transport.WriteLineAsync("uci");
				await transport.WriteLineAsync("isready");
				await transport.WriteLineAsync("quit");

				var startTime = DateTime.UtcNow;
				while (transport.LinesWritten < 3 && DateTime.UtcNow - startTime < TestConstants.DefaultTimeout)
					await Task.Delay(TestConstants.ShortDelay);

				transport.LinesWritten.Should().BeGreaterOrEqualTo(
					3,
					"All three lines should be written by the write loop");
			}

			[Fact]
			public async Task WriteLineAsync_WithLargeMessage_ShouldHandleCorrectly()
			{
				var options = new ProcessUciTransportOptions
				{
					ChannelCapacity = 1024
				};

				await using var transport = CreateTransportWithOptions(options);
				await transport.StartAsync();

				var largeMessage = new string('a', 10 * 1024);

				await FluentActions.Awaiting(() => transport.WriteLineAsync(largeMessage))
								   .Should()
								   .NotThrowAsync("Should handle large messages without error");
			}

			[Fact]
			public async Task WriteLineAsync_WithMessageAtMaxLength_ShouldSucceed()
			{
				var options = new ProcessUciTransportOptions
				{
					MaxLineLength = 1024
				};

				await using var transport = CreateTransportWithOptions(options);
				await transport.StartAsync();

				var maxLengthMessage = new string('y', 1024);

				await FluentActions.Awaiting(() => transport.WriteLineAsync(maxLengthMessage))
								   .Should()
								   .NotThrowAsync("Messages at exactly max length should be allowed");
			}

			[Fact]
			public async Task WriteLineAsync_WithMessageExceedingMaxLength_ShouldThrowArgumentException()
			{
				var options = new ProcessUciTransportOptions
				{
					MaxLineLength = 1024
				};

				await using var transport = CreateTransportWithOptions(options);
				await transport.StartAsync();

				var tooLargeMessage = new string('x', 1025);

				await FluentActions.Awaiting(() => transport.WriteLineAsync(tooLargeMessage))
								   .Should()
								   .ThrowAsync<ArgumentException>()
								   .WithMessage("*exceeds maximum length of 1024*");
			}

			[Fact]
			public async Task WriteLineAsync_WithOutgoingSingleWriterFalse_ShouldAllowConcurrentWrites()
			{
				var options = new ProcessUciTransportOptions
				{
					OutgoingSingleWriter = false
				};

				await using var transport = CreateTransportWithOptions(options);
				await transport.StartAsync();

				var write1 = transport.WriteLineAsync("uci");
				var write2 = transport.WriteLineAsync("isready");

				await Task.WhenAll(write1, write2);

				var startTime = DateTime.UtcNow;
				while (transport.LinesWritten < 2 && DateTime.UtcNow - startTime < TestConstants.DefaultTimeout)
					await Task.Delay(TestConstants.ShortDelay);

				transport.LinesWritten.Should().BeGreaterOrEqualTo(2, "Both lines should be written by the write loop");
			}

			[Fact]
			public async Task WriteLineAsync_WithOutgoingSingleWriterTrue_ShouldOptimizeForSingleWriter()
			{
				var options = new ProcessUciTransportOptions
				{
					OutgoingSingleWriter = true
				};

				await using var transport = CreateTransportWithOptions(options);
				await transport.StartAsync();

				await transport.WriteLineAsync("uci");
				await transport.WriteLineAsync("isready");

				var startTime = DateTime.UtcNow;
				while (transport.LinesWritten < 2 && DateTime.UtcNow - startTime < TestConstants.DefaultTimeout)
					await Task.Delay(TestConstants.ShortDelay);

				transport.LinesWritten.Should().BeGreaterOrEqualTo(2, "Both lines should be written by the write loop");
			}

			[Fact]
			public async Task WriteLineAsync_WithSpecialCharacters_ShouldHandleCorrectly()
			{
				await using var transport = CreateTransportWithOptions(new());
				await transport.StartAsync();

				var specialChars = "test!@#$%^&*()_+-=[]{}|;':\",./<>?";
				await FluentActions.Awaiting(() => transport.WriteLineAsync(specialChars))
								   .Should()
								   .NotThrowAsync("Should handle special characters");
			}

			[Fact]
			public async Task WriteLineAsync_WithUnicodeCharacters_ShouldHandleCorrectly()
			{
				var options = new ProcessUciTransportOptions
				{
					StdinEncoding    = Encoding.UTF8,
					ValidateCommands = false
				};

				await using var transport = CreateTransportWithOptions(options);
				await transport.StartAsync();

				const string UNICODE_MESSAGE = "test\u00E9\u00F1\u4E2D\u6587\uD83D\uDE00";

				await FluentActions.Awaiting(() => transport.WriteLineAsync(UNICODE_MESSAGE))
								   .Should()
								   .NotThrowAsync("Should handle Unicode characters with UTF-8 encoding");

				await Task.Delay(TestConstants.ShortDelay);

				transport.LinesWritten.Should().BeGreaterThan(0, "Unicode message should be written");
			}

			[Fact]
			public async Task WriteLineAsync_WithUtf8Encoding_ShouldHandleUnicodeCorrectly()
			{
				var options = new ProcessUciTransportOptions
				{
					StdinEncoding = Encoding.UTF8
				};

				await using var transport = CreateTransportWithOptions(options);
				await transport.StartAsync();

				await FluentActions.Awaiting(() => transport.WriteLineAsync("uci"))
								   .Should()
								   .NotThrowAsync("UTF-8 encoding should handle standard ASCII commands");

				await transport.DisposeAsync();
			}

			[Fact]
			public async Task WriteLineAsync_WithVeryLargeMessage_ShouldHandleCorrectly()
			{
				var options = new ProcessUciTransportOptions
				{
					ChannelCapacity = 1024
				};

				await using var transport = CreateTransportWithOptions(options);
				await transport.StartAsync();

				var veryLargeMessage = new string('b', 100 * 1024);

				await FluentActions.Awaiting(() => transport.WriteLineAsync(veryLargeMessage))
								   .Should()
								   .NotThrowAsync("Should handle very large messages without error");
			}
		}
	}
}
