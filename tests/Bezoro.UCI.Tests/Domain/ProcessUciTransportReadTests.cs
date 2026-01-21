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
[Collection("Stockfish")]
public class ProcessUciTransportReadTests(StockfishFixture fixture, ITestOutputHelper output)
	: IntegrationTestBase(fixture, output)
{
	[Fact]
	public async Task ReadLinesAsync_WhenConcurrentWithStopAsync_ShouldCompleteGracefully()
	{
		Log("Starting test: ReadLinesAsync_WhenConcurrentWithStopAsync_ShouldCompleteGracefully");
		await using var transport = Transport().Build();
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
	}

	[Fact]
	public async Task ReadLinesAsync_WhenDisposed_ShouldCompleteGracefully()
	{
		Log("Starting test: ReadLinesAsync_WhenDisposed_ShouldCompleteGracefully");
		var process = Transport().Build();
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
		Log("Starting test: ReadLinesAsync_WhenStopped_ShouldCompleteGracefully");
		await using var process = Transport().Build();
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
		Log("Starting test: ReadLinesAsync_WithSecondConcurrentReader_ShouldThrowInvalidOperationException");
		await using var process = Transport().Build();
		await process.StartAsync();

		var e1 = process.ReadLinesAsync(CancellationToken.None).GetAsyncEnumerator();
		await e1.MoveNextAsync().AsTask();

		var e2 = process.ReadLinesAsync(CancellationToken.None).GetAsyncEnumerator();
		await FluentActions.Awaiting(async () => await e2.MoveNextAsync())
						   .Should()
						   .ThrowAsync<InvalidOperationException>();
	}

	[Fact]
	public async Task ReadLinesAsync_WithSingleReader_WhenFirstDisposed_ReleasesGateForSecondReader()
	{
		Log("Starting test: ReadLinesAsync_WithSingleReader_WhenFirstDisposed_ReleasesGateForSecondReader");
		await using var process = Transport().Build();
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
	}

	[Fact]
	public async Task ReadLinesAsync_WithSingleReaderFalse_AllowsConcurrentEnumerators()
	{
		Log("Starting test: ReadLinesAsync_WithSingleReaderFalse_AllowsConcurrentEnumerators");
		await using var process = ProcessUciTransportBuilder.ForMultipleReaders().Build();
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
	}

	[Fact]
	public async Task ReadLinesAsync_WithUtf8Encoding_ShouldReadUnicodeCorrectly()
	{
		Log("Starting test: ReadLinesAsync_WithUtf8Encoding_ShouldReadUnicodeCorrectly");
		await using var transport = Transport()
									.WithStdoutEncoding(Encoding.UTF8)
									.Build();

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
	public async Task ReadLoop_WhenChannelFull_ShouldIncrementBackpressureEvents()
	{
		Log("Starting test: ReadLoop_WhenChannelFull_ShouldIncrementBackpressureEvents");
		string cmdPath = TryResolveCmdPath();

		await using var transport = Transport()
									.WithPath(cmdPath)
									.WithArguments(
										ProcessArgs.CMD_EXECUTE,
										ProcessArgs.ECHO,
										"L1",
										ProcessArgs.CHAIN,
										ProcessArgs.ECHO,
										"L2")
									.WithChannelCapacity(1)
									.Build();

		await transport.StartAsync();

		await Task.Delay(TestConstants.LongerDelay);

		await transport.StopAsync();

		transport.BackpressureEvents.Should().BeGreaterThan(0);
	}

	[Fact]
	public async Task ReadLoop_WhenEmptyLines_ShouldSkipEmptyLinesAndEmitNonEmpty()
	{
		Log("Starting test: ReadLoop_WhenEmptyLines_ShouldSkipEmptyLinesAndEmitNonEmpty");
		string cmdPath = TryResolveCmdPath();

		await using var transport = Transport()
									.WithPath(cmdPath)
									.WithArguments(
										ProcessArgs.CMD_EXECUTE,
										ProcessArgs.ECHO_EMPTY,
										ProcessArgs.CHAIN,
										ProcessArgs.ECHO,
										"marker")
									.Build();

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
	}
}
