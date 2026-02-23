using System.Text;
using Bezoro.UCI.Domain;
using Bezoro.UCI.Domain.Common.Constants;
using Bezoro.UCI.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit.Abstractions;
using static Bezoro.UCI.Tests.Domain.ProcessUciTransportTestHelpers;
using static Bezoro.UCI.Tests.TestHelpers.TestDataBuilders;

namespace Bezoro.UCI.Tests.Domain;

[TestSubject(typeof(ProcessUciTransport))]
[Trait("Category", "Integration")]
[Collection("Stockfish")]
public class ProcessUciTransportEventTests(StockfishFixture fixture, ITestOutputHelper output)
	: IntegrationTestBase(fixture, output)
{
	[Fact]
	public async Task Exited_WhenExited_ShouldBeRaisedOnlyOnce()
	{
		Log("Starting test: Exited_WhenExited_ShouldBeRaisedOnlyOnce");
		await using var transport = Transport().Build();

		var count           = 0;
		var tcs             = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
		var verificationTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

		transport.Exited += (_, _) =>
		{
			int newCount = Interlocked.Increment(ref count);
			tcs.TrySetResult(null);
			Task.Delay(TestConstants.StandardDelay).ContinueWith(_ => verificationTcs.TrySetResult(newCount));
		};

		await transport.StartAsync();
		await transport.DisposeAsync();

		var completed = await Task.WhenAny(tcs.Task, Task.Delay(TestConstants.DefaultTimeout));
		completed.Should().Be(tcs.Task, "Exited event should be raised");

		var verificationCompleted = await Task.WhenAny(
										verificationTcs.Task,
										Task.Delay(TestConstants.DefaultTimeout)
									);

		verificationCompleted.Should().Be(verificationTcs.Task, "Verification should complete");

		int finalCount = await verificationTcs.Task;
		finalCount.Should().Be(1, "Exited event should be raised exactly once");
	}

	[Fact]
	public async Task Exited_WhenHandlerThrows_ShouldRaiseErrorEventAndNotCrash()
	{
		Log("Starting test: Exited_WhenHandlerThrows_ShouldRaiseErrorEventAndNotCrash");
		await using var transport = Transport()
									.WithArguments(ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, ProcessArgs.ZERO)
									.Build();

		var errorTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
		transport.Error += ex => errorTcs.TrySetResult(ex);

		transport.Exited += (_, _) => throw new InvalidOperationException("boom from handler");

		await transport.StartAsync();

		var completed = await Task.WhenAny(errorTcs.Task, Task.Delay(TestConstants.DefaultTimeout));
		completed.Should().Be(errorTcs.Task, "Error event should be raised when Exited handler throws");

		(await errorTcs.Task).Should().BeOfType<InvalidOperationException>();
	}

	[Fact]
	public async Task Exited_WhenMultipleReaders_ShouldRaiseExitedOnce()
	{
		Log("Starting test: Exited_WhenMultipleReaders_ShouldRaiseExitedOnce");
		string cmdPath = TryResolveCmdPath();

		await using var transport = Transport()
									.WithPath(cmdPath)
									.WithArguments(ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, ProcessArgs.ZERO)
									.WithSingleReader(false)
									.Build();

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
	}

	[Fact]
	public async Task Exited_WhenProcessExits_ShouldRaiseEventWithExitCode()
	{
		Log("Starting test: Exited_WhenProcessExits_ShouldRaiseEventWithExitCode");
		string cmdPath = TryResolveCmdPath();

		await using var transport = Transport()
									.WithPath(cmdPath)
									.WithArguments(ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, ProcessArgs.ZERO)
									.Build();

		var tcs = new TaskCompletionSource<(int? Code, string? Error)>(
			TaskCreationOptions.RunContinuationsAsynchronously
		);

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
	}

	[Fact]
	public async Task Exited_WhenProcessExits_ShouldRaiseExitedEvent()
	{
		Log("Starting test: Exited_WhenProcessExits_ShouldRaiseExitedEvent");
		var tcs = new TaskCompletionSource<(int? Code, string? Error)>(
			TaskCreationOptions.RunContinuationsAsynchronously
		);

		await using var process = Transport().Build();
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
		Log("Starting test: Exited_WhenProcessExitsDuringRead_ShouldCompleteReadGracefully");
		string cmdPath = TryResolveCmdPath();

		await using var transport = Transport()
									.WithPath(cmdPath)
									.WithArguments(ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, ProcessArgs.ZERO)
									.Build();

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
	}

	[Fact]
	public async Task Exited_WhenProcessExitsDuringWrite_ShouldRaiseExitedEventAndWriteShouldFail()
	{
		Log("Starting test: Exited_WhenProcessExitsDuringWrite_ShouldRaiseExitedEventAndWriteShouldFail");
		string cmdPath = TryResolveCmdPath();

		await using var transport = Transport()
									.WithPath(cmdPath)
									.WithArguments(ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, ProcessArgs.ZERO)
									.Build();

		var exitedTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
		transport.Exited += (_, _) => exitedTcs.TrySetResult(null);

		await transport.StartAsync();

		var completed = await Task.WhenAny(exitedTcs.Task, Task.Delay(TestConstants.DefaultTimeout));
		completed.Should().Be(exitedTcs.Task, "Process should exit");

		await Task.Delay(TestConstants.StandardDelay);

		transport.IsHealthy.Should().BeFalse("IsHealthy should be false after process exits");

		await FluentActions.Awaiting(() => transport.WriteLineAsync("uci"))
						   .Should()
						   .ThrowAsync<InvalidOperationException>()
						   .WithMessage("*Engine process has exited*");
	}

	[Fact]
	public async Task IsHealthy_WhenDisposed_ShouldBeFalse()
	{
		Log("Starting test: IsHealthy_WhenDisposed_ShouldBeFalse");
		var process = Transport().Build();
		await process.StartAsync();

		await process.DisposeAsync();
		process.IsHealthy.Should().BeFalse();
	}

	[Fact]
	public async Task IsHealthy_WhenProcessExitsUnexpectedly_ShouldReturnFalse()
	{
		Log("Starting test: IsHealthy_WhenProcessExitsUnexpectedly_ShouldReturnFalse");
		string cmdPath = TryResolveCmdPath();

		await using var transport = Transport()
									.WithPath(cmdPath)
									.WithArguments(ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, ProcessArgs.ZERO)
									.Build();

		await transport.StartAsync();

		transport.IsHealthy.Should().BeTrue("Process should be healthy immediately after start");

		var exitedTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
		transport.Exited += (_, _) => exitedTcs.TrySetResult(null);

		var completed = await Task.WhenAny(exitedTcs.Task, Task.Delay(TestConstants.DefaultTimeout));
		completed.Should().Be(exitedTcs.Task, "Process should exit");

		await Task.Delay(TestConstants.ShortDelay);

		transport.IsHealthy.Should().BeFalse("IsHealthy should be false after process exits unexpectedly");
		transport.IsStarted.Should().BeTrue("IsStarted should remain true even after process exits");
	}

	[Fact]
	public async Task IsHealthy_WhenRunning_ShouldBeTrue()
	{
		Log("Starting test: IsHealthy_WhenRunning_ShouldBeTrue");
		await using var process = Transport().Build();
		await process.StartAsync();

		process.IsHealthy.Should().BeTrue();
	}

	[Fact]
	public async Task IsHealthy_WhenStopped_ShouldBeFalse()
	{
		Log("Starting test: IsHealthy_WhenStopped_ShouldBeFalse");
		await using var process = Transport().Build();
		await process.StartAsync();

		await process.StopAsync();
		process.IsHealthy.Should().BeFalse();
	}

	[Fact]
	public async Task StderrReceived_WhenHandlerThrows_ShouldSwallowExceptionAndNotCrash()
	{
		Log("Starting test: StderrReceived_WhenHandlerThrows_ShouldSwallowExceptionAndNotCrash");
		string cmdPath = TryResolveCmdPath();

		await using var transport = Transport()
									.WithPath(cmdPath)
									.WithArguments(
										ProcessArgs.CMD_EXECUTE,
										ProcessArgs.ECHO,
										"oops",
										ProcessArgs.STD_OUT_TO_STD_ERR
									)
									.WithRedirectStandardError()
									.Build();

		var errorTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
		transport.Error += ex => errorTcs.TrySetResult(ex);

		transport.StderrReceived += _ => throw new InvalidOperationException("stderr handler boom");

		await transport.StartAsync();

		var completed = await Task.WhenAny(errorTcs.Task, Task.Delay(TestConstants.BackpressureDelay));
		completed.Should().NotBe(
			errorTcs.Task,
			"stderr handler exceptions must be swallowed and not surface via Error"
		);
	}

	[Fact]
	public async Task StderrReceived_WhenRedirected_ShouldFireEvent()
	{
		Log("Starting test: StderrReceived_WhenRedirected_ShouldFireEvent");
		string cmdPath = TryResolveCmdPath();

		await using var transport = Transport()
									.WithPath(cmdPath)
									.WithArguments(
										ProcessArgs.CMD_EXECUTE,
										ProcessArgs.ECHO,
										"HelloStderr",
										ProcessArgs.STD_OUT_TO_STD_ERR
									)
									.WithRedirectStandardError()
									.Build();

		var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
		transport.StderrReceived += s =>
		{
			if (s.Contains("HelloStderr")) tcs.TrySetResult(s);
		};

		await transport.StartAsync();

		var completed = await Task.WhenAny(tcs.Task, Task.Delay(TestConstants.DefaultTimeout));
		completed.Should().Be(tcs.Task);
	}

	[Fact]
	public async Task StderrReceived_WhenStderrDisabled_ShouldNotStartStderrLoop()
	{
		Log("Starting test: StderrReceived_WhenStderrDisabled_ShouldNotStartStderrLoop");
		string cmdPath = TryResolveCmdPath();

		await using var transport = Transport()
									.WithPath(cmdPath)
									.WithArguments(
										ProcessArgs.CMD_EXECUTE,
										ProcessArgs.ECHO,
										"HelloErr",
										ProcessArgs.STD_OUT_TO_STD_ERR
									)
									.WithRedirectStandardError(false)
									.Build();

		var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
		transport.StderrReceived += s =>
		{
			if (!string.IsNullOrEmpty(s)) tcs.TrySetResult(s);
		};

		await transport.StartAsync();

		var completed = await Task.WhenAny(tcs.Task, Task.Delay(TestConstants.BackpressureDelay));
		completed.Should().NotBe(tcs.Task);
	}

	[Fact]
	public async Task StderrReceived_WithUtf8Encoding_WhenCalled_ShouldHandleUnicodeCorrectly()
	{
		Log("Starting test: StderrReceived_WithUtf8Encoding_ShouldHandleUnicodeCorrectly");
		string cmdPath = TryResolveCmdPath();

		await using var transport = Transport()
									.WithPath(cmdPath)
									.WithArguments(
										ProcessArgs.CMD_EXECUTE,
										ProcessArgs.ECHO,
										"test",
										ProcessArgs.STD_OUT_TO_STD_ERR
									)
									.WithRedirectStandardError()
									.WithStderrEncoding(Encoding.UTF8)
									.Build();

		var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
		transport.StderrReceived += s =>
		{
			if (!string.IsNullOrEmpty(s)) tcs.TrySetResult(s);
		};

		await transport.StartAsync();

		var completed = await Task.WhenAny(tcs.Task, Task.Delay(TestConstants.DefaultTimeout));
		completed.Should().Be(tcs.Task);
	}
}
