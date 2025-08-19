using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests;

[TestSubject(typeof(ProcessUciTransport))]
public class ProcessUciTransportTests
{
	public const string STOCKFISH_PATH = "Engine/stockfish/stockfish-windows-x86-64-avx2.exe";

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
	public async Task ReadLinesAsync_WhenCalledWithoutEngineStart_ThrowsInvalidOperationException()
	{
		await using var process = new ProcessUciTransport(STOCKFISH_PATH);

		var enumerator = process.ReadLinesAsync().GetAsyncEnumerator();

		await FluentActions
			  .Awaiting(async () => await enumerator.MoveNextAsync())
			  .Should()
			  .ThrowAsync<InvalidOperationException>();
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
	public async Task WriteLineAsync_WhenCalledWithEngineStart_WritesLineToEngine()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return; // No engine available -> skip test run without false failure.

		await using var process = new ProcessUciTransport(path);
		await process.StartAsync();

		await process.WriteLineAsync("uci");

		string? output = null;

		await foreach (string line in process.ReadLinesAsync(0, CancellationToken.None))
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
		await using var process = new ProcessUciTransport(STOCKFISH_PATH);
		await process.StartAsync();

		await FluentActions
			  .Awaiting(() => process.WriteLineAsync("uci\nisready"))
			  .Should()
			  .ThrowAsync<ArgumentException>();
	}

	[Fact]
	public async Task WriteLineAsync_WhenCalledWithNull_ThrowsArgumentNullException()
	{
		await using var process = new ProcessUciTransport(STOCKFISH_PATH);
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
		await using var process = new ProcessUciTransport(STOCKFISH_PATH);

		await FluentActions
			  .Awaiting(() => process.WriteLineAsync("uci"))
			  .Should()
			  .ThrowAsync<InvalidOperationException>();
	}

	[Fact]
	public async Task WriteLineAsync_WhenCalledWithWhitespace_ThrowsArgumentException()
	{
		await using var process = new ProcessUciTransport(STOCKFISH_PATH);
		await process.StartAsync();

		await FluentActions
			  .Awaiting(() => process.WriteLineAsync("   "))
			  .Should()
			  .ThrowAsync<ArgumentException>();
	}

	private static string? TryResolveEnginePath()
	{
		string? fromEnv = Environment.GetEnvironmentVariable("STOCKFISH_PATH");
		if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv))
			return fromEnv;

		return File.Exists(STOCKFISH_PATH) ? STOCKFISH_PATH : null;
	}
}
