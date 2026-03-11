using Bezoro.UCI.Domain;
using Bezoro.UCI.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;
using NSubstitute;

namespace Bezoro.UCI.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientIsReadyTests
{
	[Fact]
	public async Task IsReadyAsync_WhenCancelled_ShouldThrowOperationCanceledException()
	{
		// Arrange
		var (_, client) = UciEngineClientTestHelpers.CreateClientWithTransport();
		var cts = new CancellationTokenSource(TestConstants.CancellationTimeout);

		// Act + Assert
		await FluentActions
			  .Awaiting(() => client.IsReadyAsync(cts.Token))
			  .Should()
			  .ThrowAsync<OperationCanceledException>("operation should be cancelled");
	}

	[Fact(Timeout = 4000)]
	public async Task IsReadyAsync_WhenCalledConcurrently_ShouldSerializeReadyChecks()
	{
		// Arrange
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client = await UciEngineClientTestHelpers.StartClientWithHandshakeAsync(transport, channel);

		var firstReadyResponseGate  = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var secondReadyResponseGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var firstCommandIssued      = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var secondCommandIssued     = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var readyCommandCount       = 0;

		transport.ClearReceivedCalls();
		transport.When(x => x.WriteLineAsync("isready", Arg.Any<CancellationToken>()))
				 .Do(
					 async _ =>
					 {
						 int invocation = Interlocked.Increment(ref readyCommandCount);
						 if (invocation == 1)
						 {
							 firstCommandIssued.TrySetResult();
							 await firstReadyResponseGate.Task;
						 }
						 else
						 {
							 secondCommandIssued.TrySetResult();
							 await secondReadyResponseGate.Task;
						 }

						 await channel.Writer.WriteAsync("readyok");
					 }
				 );

		// Act
		Task firstReadyTask  = client.IsReadyAsync(CancellationToken.None);
		Task secondReadyTask = client.IsReadyAsync(CancellationToken.None);

		// Assert
		await firstCommandIssued.Task.WaitAsync(TestConstants.DefaultTimeout);
		await Task.Delay(TestConstants.ShortDelay);

		readyCommandCount.Should().Be(1, "the second ready check should wait for the first response");
		secondReadyTask.IsCompleted.Should().BeFalse();

		firstReadyResponseGate.TrySetResult();
		await firstReadyTask.WaitAsync(TestConstants.DefaultTimeout);
		await secondCommandIssued.Task.WaitAsync(TestConstants.DefaultTimeout);

		readyCommandCount.Should().Be(2, "each concurrent call should issue its own ready check");

		secondReadyResponseGate.TrySetResult();
		await secondReadyTask.WaitAsync(TestConstants.DefaultTimeout);
	}
}
