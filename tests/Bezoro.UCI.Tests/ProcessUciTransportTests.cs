using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests;

[TestSubject(typeof(ProcessUciTransport))]
public class ProcessUciTransportTests
{
	public const string STOCKFISH_PATH = "Engine/stockfish/stockfish-windows-x86-64-avx2.exe";

	[Fact]
	public async Task StartAsync_WhenCalledWithInvalidProcess_ThrowsException()
	{
		var process = new ProcessUciTransport("invalid/path");

		await FluentActions.Awaiting(() => process.StartAsync()).Should().ThrowAsync<Exception>();
	}

	[Fact]
	public async Task StartAsync_WhenCalledWithValidProcess_StartsProcess()
	{
		var process = new ProcessUciTransport(STOCKFISH_PATH);

		await process.StartAsync();

		process.IsStarted.Should().BeTrue();
	}

	[Fact]
	public async Task WriteLineAsync_WhenCalledWithEngineStart_WritesLineToEngine()
	{
		var process = new ProcessUciTransport(STOCKFISH_PATH);
		await process.StartAsync();

		await process.WriteLineAsync("uci");

		var output = "";
		var lines  = process.ReadLinesAsync();
		await foreach (string line in lines)
		{
			if (line != "uciok") continue;

			output = line;
			break;
		}

		output.Should().Be("uciok");
	}

	[Fact]
	public async Task WriteLineAsync_WhenCalledWithoutEngineStart_ThrowsException()
	{
		var process = new ProcessUciTransport(STOCKFISH_PATH);

		await FluentActions
			  .Awaiting(() => process.WriteLineAsync("uci"))
			  .Should()
			  .ThrowAsync<InvalidOperationException>();
	}
}
