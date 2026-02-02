using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.GameSystems.ActivationSystem.Services;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.ActivationSystem;

[TestSubject(typeof(ActivationService))]
public class ActivationServiceBudgetTests
{
	[Fact]
	public async Task WhenLargeBudget_ShouldActivateAllQuickly()
	{
		using var service   = new ActivationService();
		var       activated = 0;

		for (var i = 0; i < 50; i++)
			service.Register(() => Interlocked.Increment(ref activated));

		service.Start(
			new(
				100,
				10
			)
		);

		await Task.Delay(200);

		Volatile.Read(ref activated).Should().Be(50);
	}

	[Fact]
	public async Task WhenMaxBatchSize_ShouldNotExceedMax()
	{
		using var service   = new ActivationService();
		var       activated = 0;

		for (var i = 0; i < 100; i++)
			service.Register(() => Interlocked.Increment(ref activated));

		// Large budget but maxBatchSize = 5
		service.Start(
			new(
				1000,
				1000,
				maxBatchSize: 5
			)
		);

		// Wait for the first activation, with a long delay between iterations to avoid racey counts.
		var sw = Stopwatch.StartNew();
		while (Volatile.Read(ref activated) == 0 && sw.ElapsedMilliseconds < 500)
			await Task.Delay(5);

		service.Stop();

		// Should have activated at most maxBatchSize per iteration
		Volatile.Read(ref activated).Should().BeGreaterThan(0);
		Volatile.Read(ref activated).Should().BeLessThanOrEqualTo(5);
	}

	[Fact]
	public async Task WhenMinBatchSize_ShouldActivateAtLeastMinPerIteration()
	{
		using var service   = new ActivationService();
		var       activated = 0;

		for (var i = 0; i < 10; i++)
		{
			service.Register(() =>
				{
					// Simulate slow callback registration
					Interlocked.Increment(ref activated);
				}
			);
		}

		// Very tiny budget but minBatchSize = 3
		service.Start(
			new(
				0.0001,
				10,
				3,
				3
			)
		);

		// Wait for one iteration to complete
		await Task.Delay(100);
		service.Stop();

		// Should have activated at least minBatchSize items
		Volatile.Read(ref activated).Should().BeGreaterThanOrEqualTo(3);
	}
}
