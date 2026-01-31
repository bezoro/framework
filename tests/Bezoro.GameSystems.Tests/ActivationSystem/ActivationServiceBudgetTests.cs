using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.GameSystems.ActivationSystem.Services;
using Bezoro.GameSystems.ActivationSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.ActivationSystem;

[TestSubject(typeof(ActivationService))]
public class ActivationServiceBudgetTests
{
	[Fact]
	public async Task WhenMinBatchSize_ShouldActivateAtLeastMinPerIteration()
	{
		using var service = new ActivationService();
		var activated = 0;

		for (var i = 0; i < 10; i++)
		{
			service.Register(() =>
			{
				// Simulate slow callback registration
				Interlocked.Increment(ref activated);
			});
		}

		// Very tiny budget but minBatchSize = 3
		service.Start(new ActivationConfig(
			timeBudgetMs: 0.0001,
			iterationDelayMs: 10,
			minBatchSize: 3,
			maxBatchSize: 3
		));

		// Wait for one iteration to complete
		await Task.Delay(100);
		service.Stop();

		// Should have activated at least minBatchSize items
		Volatile.Read(ref activated).Should().BeGreaterThanOrEqualTo(3);
	}

	[Fact]
	public async Task WhenMaxBatchSize_ShouldNotExceedMax()
	{
		using var service = new ActivationService();
		var activated = 0;

		for (var i = 0; i < 100; i++)
			service.Register(() => Interlocked.Increment(ref activated));

		// Large budget but maxBatchSize = 5
		service.Start(new ActivationConfig(
			timeBudgetMs: 1000,
			iterationDelayMs: 50,
			maxBatchSize: 5
		));

		// Wait for exactly one iteration
		await Task.Delay(80);
		service.Stop();

		// Should have activated at most maxBatchSize per iteration
		Volatile.Read(ref activated).Should().BeLessThanOrEqualTo(10); // Allow for 2 iterations
	}

	[Fact]
	public async Task WhenLargeBudget_ShouldActivateAllQuickly()
	{
		using var service = new ActivationService();
		var activated = 0;

		for (var i = 0; i < 50; i++)
			service.Register(() => Interlocked.Increment(ref activated));

		service.Start(new ActivationConfig(
			timeBudgetMs: 100,
			iterationDelayMs: 10
		));

		await Task.Delay(200);

		Volatile.Read(ref activated).Should().Be(50);
	}
}
