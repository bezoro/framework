using System.Text;
using Bezoro.Chess.UCI.Protocol.Domain.Common.Constants;
using Bezoro.Chess.UCI.Protocol.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit.Abstractions;
using static Bezoro.Chess.UCI.Protocol.Tests.TestHelpers.TestDataBuilders;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain;

[TestSubject(typeof(ProcessUciTransport))]
[Trait("Category", "Integration")]
[Collection("Stockfish")]
public class ProcessUciTransportWriteTests(StockfishFixture fixture, ITestOutputHelper output)
	: IntegrationTestBase(fixture, output)
{
	[Fact]
	public async Task TryWriteLineAsync_WhenCanceledDuringWait_ShouldThrowOperationCanceledException()
	{
		Log("Starting test: TryWriteLineAsync_WhenCanceledDuringWait_ShouldThrowOperationCanceledException");
		await using var transport = ProcessUciTransportBuilder.ForBackpressureTest()
															  .WithValidateCommands()
															  .Build();

		await transport.StartAsync();

		bool ok1 = await transport.TryWriteLineAsync("uci", TestConstants.SmallTimeout);
		ok1.Should().BeTrue();

		var cts = new CancellationTokenSource(TestConstants.MediumDelay);
		await FluentActions
			  .Awaiting(() => transport.TryWriteLineAsync("isready", TestConstants.DefaultTimeout, cts.Token))
			  .Should()
			  .ThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task TryWriteLineAsync_WhenChannelClosedDuringStop_ShouldThrowInvalidOperationException()
	{
		Log("Starting test: TryWriteLineAsync_WhenChannelClosedDuringStop_ShouldThrowInvalidOperationException");
		await using var transport = ProcessUciTransportBuilder.ForBackpressureTest().Build();

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
		Log("Starting test: TryWriteLineAsync_WhenChannelFullAndTinyTimeout_ShouldSpinAndReturnFalse");
		await using var transport = ProcessUciTransportBuilder.ForBackpressureTest()
															  .WithSmallTimeoutSpinIterations(10)
															  .Build();

		await transport.StartAsync();

		bool ok1 = await transport.TryWriteLineAsync("uci", TestConstants.TinyTimeout);
		ok1.Should().BeTrue();

		bool ok2 = await transport.TryWriteLineAsync("isready", TestConstants.VeryShortDelay);
		ok2.Should().BeFalse();
	}

	[Fact]
	public async Task TryWriteLineAsync_WhenChannelFullAndZeroTimeout_ShouldReturnFalse()
	{
		Log("Starting test: TryWriteLineAsync_WhenChannelFullAndZeroTimeout_ShouldReturnFalse");
		await using var transport = ProcessUciTransportBuilder.ForBackpressureTest()
															  .WithValidateCommands()
															  .Build();

		await transport.StartAsync();

		bool ok1 = await transport.TryWriteLineAsync("uci", TestConstants.TinyTimeout);
		ok1.Should().BeTrue();

		bool ok2 = await transport.TryWriteLineAsync("isready", TimeSpan.Zero);
		ok2.Should().BeFalse();
	}

	[Fact]
	public async Task TryWriteLineAsync_WhenChannelReady_ShouldReturnTrue()
	{
		Log("Starting test: TryWriteLineAsync_WhenChannelReady_ShouldReturnTrue");
		await using var process = Transport().WithTeardownTimeout(TestConstants.ShortTimeout)
											 .WithQuitGracePeriod(TestConstants.QuitGracePeriod).Build();

		await process.StartAsync();

		bool ok = await process.TryWriteLineAsync("uci", TestConstants.ShortTimeout);
		ok.Should().BeTrue();
	}

	[Fact]
	public async Task TryWriteLineAsync_WhenDisposed_ShouldThrowInvalidOperationException()
	{
		Log("Starting test: TryWriteLineAsync_WhenDisposed_ShouldThrowInvalidOperationException");
		var process = Transport().Build();
		await process.StartAsync();
		await process.DisposeAsync();

		await FluentActions.Awaiting(() => process.TryWriteLineAsync("uci", TestConstants.TinyTimeout))
						   .Should()
						   .ThrowAsync<InvalidOperationException>();
	}

	[Fact]
	public async Task TryWriteLineAsync_WhenInfiniteTimeoutAndChannelFull_ShouldComplete()
	{
		Log("Starting test: TryWriteLineAsync_WhenInfiniteTimeoutAndChannelFull_ShouldComplete");
		await using var transport = Transport()
									.WithChannelCapacity(1)
									.WithDisableWriteLoop(false)
									.Build();

		await transport.StartAsync();

		bool ok1 = await transport.TryWriteLineAsync("uci", TestConstants.TinyTimeout);
		ok1.Should().BeTrue();

		bool ok2 = await transport.TryWriteLineAsync("isready", Timeout.InfiniteTimeSpan);
		ok2.Should().BeTrue();
	}

	[Fact]
	public async Task TryWriteLineAsync_WhenNegativeNonInfiniteTimeout_ShouldThrowArgumentOutOfRangeException()
	{
		Log("Starting test: TryWriteLineAsync_WhenNegativeNonInfiniteTimeout_ShouldThrowArgumentOutOfRangeException");
		await using var process = Transport().Build();
		await process.StartAsync();

		await FluentActions.Awaiting(() => process.TryWriteLineAsync("uci", TimeSpan.FromMilliseconds(-2)))
						   .Should()
						   .ThrowAsync<ArgumentOutOfRangeException>();
	}

	[Fact]
	public async Task TryWriteLineAsync_WhenNotStarted_ShouldThrowInvalidOperationException()
	{
		Log("Starting test: TryWriteLineAsync_WhenNotStarted_ShouldThrowInvalidOperationException");
		var process = Transport().WithPath("any/nonempty/path").Build();

		await FluentActions.Awaiting(() => process.TryWriteLineAsync("uci", TestConstants.TinyTimeout))
						   .Should()
						   .ThrowAsync<InvalidOperationException>();
	}

	[Fact]
	public async Task TryWriteLineAsync_WhenNullLine_ShouldThrowArgumentNullException()
	{
		Log("Starting test: TryWriteLineAsync_WhenNullLine_ShouldThrowArgumentNullException");
		await using var process = Transport().Build();
		await process.StartAsync();

		await FluentActions
			  .Awaiting(() => process.TryWriteLineAsync(null!, TestConstants.TinyTimeout))
			  .Should()
			  .ThrowAsync<ArgumentNullException>();
	}

	[Fact]
	public async Task TryWriteLineAsync_WhenProcessHasExited_ShouldThrowInvalidOperationException()
	{
		Log("Starting test: TryWriteLineAsync_WhenProcessHasExited_ShouldThrowInvalidOperationException");
		await using var transport = Transport()
									.WithArguments(ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, ProcessArgs.ZERO)
									.Build();

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
		Log("Starting test: TryWriteLineAsync_WhenStopped_ShouldThrowInvalidOperationException");
		await using var process = Transport().Build();
		await process.StartAsync();
		await process.StopAsync();

		await FluentActions.Awaiting(() => process.TryWriteLineAsync("uci", TestConstants.TinyTimeout))
						   .Should()
						   .ThrowAsync<InvalidOperationException>();
	}

	[Fact]
	public async Task TryWriteLineAsync_WhenTimeout_ShouldReturnFalseAfterTimeout()
	{
		Log("Starting test: TryWriteLineAsync_WhenTimeout_ShouldReturnFalseAfterTimeout");
		await using var transport = ProcessUciTransportBuilder.ForBackpressureTest().Build();
		await transport.StartAsync();

		bool firstOk = await transport.TryWriteLineAsync("uci", TestConstants.MediumDelay);
		firstOk.Should().BeTrue();

		bool ok = await transport.TryWriteLineAsync("isready", TestConstants.SmallTimeout);
		ok.Should().BeFalse();
	}

	[Fact]
	public async Task TryWriteLineAsync_WithMessageExceedingMaxLength_WhenCalled_ShouldThrowArgumentException()
	{
		Log("Starting test: TryWriteLineAsync_WithMessageExceedingMaxLength_ShouldThrowArgumentException");
		await using var transport = Transport()
									.WithMaxLineLength(2048)
									.Build();

		await transport.StartAsync();

		var tooLargeMessage = new string('z', 2049);

		await FluentActions
			  .Awaiting(() => transport.TryWriteLineAsync(tooLargeMessage, TimeSpan.FromSeconds(1)))
			  .Should()
			  .ThrowAsync<ArgumentException>()
			  .WithMessage("*exceeds maximum length of 2048*");
	}

	[Fact]
	public async Task WriteLineAsync_WhenCalledWithNewline_ShouldThrowArgumentException()
	{
		Log("Starting test: WriteLineAsync_WhenCalledWithNewline_ShouldThrowArgumentException");
		await using var process = Transport().Build();
		await process.StartAsync();

		await FluentActions
			  .Awaiting(() => process.WriteLineAsync("uci\nisready"))
			  .Should()
			  .ThrowAsync<ArgumentException>();
	}

	[Fact]
	public async Task WriteLineAsync_WhenCanceled_ShouldThrowOperationCanceledException()
	{
		Log("Starting test: WriteLineAsync_WhenCanceled_ShouldThrowOperationCanceledException");
		await using var process = Transport().Build();
		await process.StartAsync();

		var cts = new CancellationTokenSource();
		cts.Cancel();

		await FluentActions.Awaiting(() => process.WriteLineAsync("uci", cts.Token))
						   .Should()
						   .ThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task WriteLineAsync_WhenDisposed_ShouldThrowInvalidOperationException()
	{
		Log("Starting test: WriteLineAsync_WhenDisposed_ShouldThrowInvalidOperationException");
		var process = Transport().Build();
		await process.StartAsync();
		await process.DisposeAsync();

		await FluentActions.Awaiting(() => process.WriteLineAsync("uci"))
						   .Should()
						   .ThrowAsync<InvalidOperationException>();
	}

	[Fact]
	public async Task WriteLineAsync_WhenEngineStarted_ShouldWriteLineToEngine()
	{
		Log("Starting test: WriteLineAsync_WhenEngineStarted_ShouldWriteLineToEngine");
		await using var process = Transport().Build();
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
			"the engine should acknowledge UCI initialization with 'uciok' within timeout"
		);
	}

	[Fact]
	public async Task WriteLineAsync_WhenNotStarted_ShouldThrowInvalidOperationException()
	{
		Log("Starting test: WriteLineAsync_WhenNotStarted_ShouldThrowInvalidOperationException");
		var process = Transport().WithPath("any/nonempty/path").Build();

		await FluentActions
			  .Awaiting(() => process.WriteLineAsync("uci"))
			  .Should()
			  .ThrowAsync<InvalidOperationException>();
	}

	[Fact]
	public async Task WriteLineAsync_WhenNull_ShouldThrowArgumentNullException()
	{
		Log("Starting test: WriteLineAsync_WhenNull_ShouldThrowArgumentNullException");
		await using var process = Transport().Build();
		await process.StartAsync();

		await FluentActions
			  .Awaiting(() => process.WriteLineAsync(null!))
			  .Should()
			  .ThrowAsync<ArgumentNullException>();
	}

	[Fact]
	public async Task WriteLineAsync_WhenProcessHasExited_ShouldThrowInvalidOperationException()
	{
		Log("Starting test: WriteLineAsync_WhenProcessHasExited_ShouldThrowInvalidOperationException");
		await using var transport = Transport()
									.WithArguments(ProcessArgs.CMD_EXECUTE, ProcessArgs.EXIT, ProcessArgs.ZERO)
									.Build();

		var exitedTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
		transport.Exited += (_, _) => exitedTcs.TrySetResult(null);

		await transport.StartAsync();

		var completed = await Task.WhenAny(exitedTcs.Task, Task.Delay(TestConstants.DefaultTimeout));
		completed.Should().Be(exitedTcs.Task);

		await FluentActions.Awaiting(() => transport.WriteLineAsync("uci"))
						   .Should()
						   .ThrowAsync<InvalidOperationException>();
	}

	[Fact]
	public async Task WriteLineAsync_WhenStopped_ShouldThrowInvalidOperationException()
	{
		Log("Starting test: WriteLineAsync_WhenStopped_ShouldThrowInvalidOperationException");
		await using var process = Transport().Build();
		await process.StartAsync();
		await process.StopAsync();

		await FluentActions.Awaiting(() => process.WriteLineAsync("uci"))
						   .Should()
						   .ThrowAsync<InvalidOperationException>();
	}

	[Fact]
	public async Task WriteLineAsync_WhenUnknownCommand_ShouldNotThrowAndReadingContinues()
	{
		Log("Starting test: WriteLineAsync_WhenUnknownCommand_ShouldNotThrowAndReadingContinues");
		await using var process = Transport().Build();
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
	}

	[Fact]
	public async Task WriteLineAsync_WhenValidationDisabled_ShouldAllowWhitespaceAndNewline()
	{
		Log("Starting test: WriteLineAsync_WhenValidationDisabled_ShouldAllowWhitespaceAndNewline");
		await using var process = Transport()
								  .WithValidateCommands(false)
								  .Build();

		await process.StartAsync();

		await FluentActions.Awaiting(() => process.WriteLineAsync("   "))
						   .Should()
						   .NotThrowAsync();

		await FluentActions.Awaiting(() => process.WriteLineAsync("uci\n"))
						   .Should()
						   .NotThrowAsync();
	}

	[Fact]
	public async Task WriteLineAsync_WhenWhitespace_ShouldThrowArgumentException()
	{
		Log("Starting test: WriteLineAsync_WhenWhitespace_ShouldThrowArgumentException");
		await using var process = Transport().Build();
		await process.StartAsync();

		await FluentActions
			  .Awaiting(() => process.WriteLineAsync("   "))
			  .Should()
			  .ThrowAsync<ArgumentException>();
	}

	[Fact]
	public async Task WriteLineAsync_WithCarriageReturn_WhenCalled_ShouldThrowArgumentException()
	{
		Log("Starting test: WriteLineAsync_WithCarriageReturn_ShouldThrowArgumentException");
		await using var process = Transport().Build();
		await process.StartAsync();

		await FluentActions.Awaiting(() => process.WriteLineAsync("uci\r"))
						   .Should()
						   .ThrowAsync<ArgumentException>();
	}

	[Fact]
	public async Task WriteLineAsync_WithFlushBatchSize_WhenCalled_ShouldRespectBatching()
	{
		Log("Starting test: WriteLineAsync_WithFlushBatchSize_ShouldRespectBatching");
		await using var transport = Transport()
									.WithFlushBatchSize(3)
									.Build();

		await transport.StartAsync();

		await transport.WriteLineAsync("uci");
		await transport.WriteLineAsync("isready");
		await transport.WriteLineAsync("quit");

		var startTime = DateTime.UtcNow;
		while (transport.LinesWritten < 3 && DateTime.UtcNow - startTime < TestConstants.DefaultTimeout)
			await Task.Delay(TestConstants.ShortDelay);

		transport.LinesWritten.Should().BeGreaterOrEqualTo(
			3,
			"All three lines should be written by the write loop"
		);
	}

	[Fact]
	public async Task WriteLineAsync_WithMessageAtMaxLength_WhenCalled_ShouldSucceed()
	{
		Log("Starting test: WriteLineAsync_WithMessageAtMaxLength_ShouldSucceed");
		await using var transport = Transport()
									.WithMaxLineLength(1024)
									.Build();

		await transport.StartAsync();

		var maxLengthMessage = new string('y', 1024);

		await FluentActions.Awaiting(() => transport.WriteLineAsync(maxLengthMessage))
						   .Should()
						   .NotThrowAsync("Messages at exactly max length should be allowed");
	}

	[Fact]
	public async Task WriteLineAsync_WithMessageExceedingMaxLength_WhenCalled_ShouldThrowArgumentException()
	{
		Log("Starting test: WriteLineAsync_WithMessageExceedingMaxLength_ShouldThrowArgumentException");
		await using var transport = Transport()
									.WithMaxLineLength(1024)
									.Build();

		await transport.StartAsync();

		var tooLargeMessage = new string('x', 1025);

		await FluentActions.Awaiting(() => transport.WriteLineAsync(tooLargeMessage))
						   .Should()
						   .ThrowAsync<ArgumentException>()
						   .WithMessage("*exceeds maximum length of 1024*");
	}

	[Fact]
	public async Task WriteLineAsync_WithOutgoingSingleWriterFalse_WhenCalled_ShouldAllowConcurrentWrites()
	{
		Log("Starting test: WriteLineAsync_WithOutgoingSingleWriterFalse_ShouldAllowConcurrentWrites");
		await using var transport = Transport()
									.WithOutgoingSingleWriter(false)
									.Build();

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
	public async Task WriteLineAsync_WithOutgoingSingleWriterTrue_WhenCalled_ShouldOptimizeForSingleWriter()
	{
		Log("Starting test: WriteLineAsync_WithOutgoingSingleWriterTrue_ShouldOptimizeForSingleWriter");
		await using var transport = Transport()
									.WithOutgoingSingleWriter()
									.Build();

		await transport.StartAsync();

		await transport.WriteLineAsync("uci");
		await transport.WriteLineAsync("isready");

		var startTime = DateTime.UtcNow;
		while (transport.LinesWritten < 2 && DateTime.UtcNow - startTime < TestConstants.DefaultTimeout)
			await Task.Delay(TestConstants.ShortDelay);

		transport.LinesWritten.Should().BeGreaterOrEqualTo(2, "Both lines should be written by the write loop");
	}

	[Fact]
	public async Task WriteLineAsync_WithSpecialCharacters_WhenCalled_ShouldHandleCorrectly()
	{
		Log("Starting test: WriteLineAsync_WithSpecialCharacters_ShouldHandleCorrectly");
		await using var transport = Transport()
									.WithTeardownTimeout(TestConstants.ShortTimeout)
									.WithQuitGracePeriod(TestConstants.QuitGracePeriod)
									.Build();

		await transport.StartAsync();

		var specialChars = "test!@#$%^&*()_+-=[]{}|;':\",./<>?";
		await FluentActions.Awaiting(() => transport.WriteLineAsync(specialChars))
						   .Should()
						   .NotThrowAsync("Should handle special characters");
	}

	[Fact]
	public async Task WriteLineAsync_WithUnicodeCharacters_WhenCalled_ShouldHandleCorrectly()
	{
		Log("Starting test: WriteLineAsync_WithUnicodeCharacters_ShouldHandleCorrectly");
		await using var transport = Transport()
									.WithStdinEncoding(Encoding.UTF8)
									.WithValidateCommands(false)
									.Build();

		await transport.StartAsync();

		const string UNICODE_MESSAGE = "test\u00E9\u00F1\u4E2D\u6587\uD83D\uDE00";

		await FluentActions.Awaiting(() => transport.WriteLineAsync(UNICODE_MESSAGE))
						   .Should()
						   .NotThrowAsync("Should handle Unicode characters with UTF-8 encoding");

		await Task.Delay(TestConstants.ShortDelay);

		transport.LinesWritten.Should().BeGreaterThan(0, "Unicode message should be written");
	}

	[Fact]
	public async Task WriteLineAsync_WithUtf8Encoding_WhenCalled_ShouldHandleUnicodeCorrectly()
	{
		Log("Starting test: WriteLineAsync_WithUtf8Encoding_ShouldHandleUnicodeCorrectly");
		await using var transport = Transport()
									.WithStdinEncoding(Encoding.UTF8)
									.Build();

		await transport.StartAsync();

		await FluentActions.Awaiting(() => transport.WriteLineAsync("uci"))
						   .Should()
						   .NotThrowAsync("UTF-8 encoding should handle standard ASCII commands");
	}
}
