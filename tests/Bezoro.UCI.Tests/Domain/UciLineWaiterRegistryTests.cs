using Bezoro.UCI.Domain.EngineClient;
using Bezoro.UCI.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Domain;

[TestSubject(typeof(UciLineWaiterRegistry))]
public class UciLineWaiterRegistryTests
{
	[Fact]
	public async Task Notify_WhenMultipleWaitersMatchSameLine_ShouldCompleteOneWaiterPerNotification()
	{
		// Arrange
		var registry = new UciLineWaiterRegistry();

		Task<string> firstWaiter = registry.WaitForAsync(
			static line => line == "readyok",
			Timeout.InfiniteTimeSpan,
			CancellationToken.None
		);

		Task<string> secondWaiter = registry.WaitForAsync(
			static line => line == "readyok",
			Timeout.InfiniteTimeSpan,
			CancellationToken.None
		);

		// Act
		registry.Notify("readyok");

		// Assert
		string firstResult = await firstWaiter.WaitAsync(TestConstants.DefaultTimeout);
		firstResult.Should().Be("readyok");
		secondWaiter.IsCompleted.Should().BeFalse("a single line should satisfy only one waiter");

		registry.Notify("readyok");

		string secondResult = await secondWaiter.WaitAsync(TestConstants.DefaultTimeout);
		secondResult.Should().Be("readyok");
	}

	[Fact]
	public async Task Notify_WhenFirstWaiterIsCancelled_ShouldSkipItAndCompleteNextMatchingWaiter()
	{
		// Arrange
		var registry = new UciLineWaiterRegistry();
		using var canceledCts = new CancellationTokenSource();

		Task<string> canceledWaiter = registry.WaitForAsync(
			static line => line == "readyok",
			Timeout.InfiniteTimeSpan,
			canceledCts.Token
		);

		Task<string> activeWaiter = registry.WaitForAsync(
			static line => line == "readyok",
			Timeout.InfiniteTimeSpan,
			CancellationToken.None
		);

		canceledCts.Cancel();
		await FluentActions.Awaiting(() => canceledWaiter).Should().ThrowAsync<OperationCanceledException>();

		// Act
		registry.Notify("readyok");

		// Assert
		string result = await activeWaiter.WaitAsync(TestConstants.DefaultTimeout);
		result.Should().Be("readyok");
	}
}
