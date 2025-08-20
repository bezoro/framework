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
	public async Task DisposeAsync_BeforeStart_IsNoOpAndDoesNotThrow()
	{
		await using var process = new ProcessUciTransport("any/nonempty/path");
		await process.Awaiting(p => p.DisposeAsync().AsTask()).Should().NotThrowAsync();
		process.IsStarted.Should().BeFalse();
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
	public async Task WriteLineAsync_WhenCalledWithEngineStart_WritesLineToEngine()
	{
		string? path = TryResolveEnginePath();
		if (path is null) return; // No engine available -> skip test run without false failure.

		await using var process = new ProcessUciTransport(path);
		await process.StartAsync();

		await process.WriteLineAsync("uci");

		string? output = null;

		await foreach (string line in process.ReadLinesAsync(CancellationToken.None))
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

	private static string? TryResolveEnginePath()
	{
		string? fromEnv = Environment.GetEnvironmentVariable("STOCKFISH_PATH");
		if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv))
			return fromEnv;

		return File.Exists(STOCKFISH_PATH) ? STOCKFISH_PATH : null;
	}
}
