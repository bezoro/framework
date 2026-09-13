using Bezoro.Chess.UCI.Protocol.Internal;
using Bezoro.Chess.UCI.Protocol.Tests.TestHelpers;
using FluentAssertions;
using NSubstitute;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain;

public sealed class UciPonderRuntimeTests
{
	[Fact]
	public async Task StopSearchAsync_WhenStartSearchIsWritingPosition_ShouldWaitForStartOperation()
	{
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client = await UciEngineClientTestHelpers.StartClientWithHandshakeAsync(transport, channel);
		await using var runtime = new UciPonderRuntime(client);
		transport.IsStarted.Returns(true);

		var positionWritten = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var releasePosition = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var stopWritten     = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		transport.ClearReceivedCalls();
		transport.WriteLineAsync(
				 Arg.Is<string>(static line => line.StartsWith("position ", StringComparison.Ordinal)),
				 Arg.Any<CancellationToken>())
			 .Returns(async _ =>
				 {
					 positionWritten.TrySetResult();
					 await releasePosition.Task;
				 }
			 );
		transport.When(static value => value.WriteLineAsync("stop", Arg.Any<CancellationToken>()))
				 .Do(_ => stopWritten.TrySetResult());
		transport.When(static value => value.WriteLineAsync("isready", Arg.Any<CancellationToken>()))
				 .Do(async _ => await channel.Writer.WriteAsync("readyok"));

		var startTask = runtime.StartSearchAsync(Fen.Default, null);
		await positionWritten.Task.WaitAsync(TestConstants.DefaultTimeout);

		var stopTask = runtime.StopSearchAsync();
		await Task.Delay(TestConstants.ShortDelay);

		stopWritten.Task.IsCompleted.Should().BeFalse(
			"a stop command must not interleave with an in-progress position/go sequence"
		);

		releasePosition.TrySetResult();
		await Task.WhenAll(startTask, stopTask).WaitAsync(TestConstants.DefaultTimeout);
	}
}
