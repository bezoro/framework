using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.GameSystems.ActivationSystem.Services;
using Bezoro.GameSystems.ActivationSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.ActivationSystem;

[TestSubject(typeof(ActivationService))]
public class ActivationServiceConcurrencyTests
{
	[Fact]
	public async Task WhenConcurrentRegisterAndCancel_ShouldNotThrow()
	{
		using var service = new ActivationService();
		service.Start(new ActivationConfig(timeBudgetMs: 10, iterationDelayMs: 10));

		var tasks = new List<Task>();

		for (var i = 0; i < 10; i++)
		{
			tasks.Add(
				Task.Run(() =>
				{
					for (var j = 0; j < 50; j++)
					{
						var handle = service.Register(() => { });
						Thread.Sleep(1);
						service.Cancel(handle);
					}
				}));
		}

		var act = async () => await Task.WhenAll(tasks);

		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task WhenConcurrentRegistrationDuringProcessing_ShouldNotThrow()
	{
		using var service = new ActivationService();
		var activated = 0;

		// Pre-register some items
		for (var i = 0; i < 50; i++)
			service.Register(() => Interlocked.Increment(ref activated));

		service.Start(new ActivationConfig(timeBudgetMs: 5, iterationDelayMs: 10));

		var tasks = new List<Task>();

		// Concurrently register more items while processing
		for (var i = 0; i < 10; i++)
		{
			tasks.Add(
				Task.Run(() =>
				{
					for (var j = 0; j < 20; j++)
					{
						service.Register(() => Interlocked.Increment(ref activated));
						Thread.Sleep(5);
					}
				}));
		}

		var act = async () => await Task.WhenAll(tasks);

		await act.Should().NotThrowAsync();

		// Wait for processing to complete
		await Task.Delay(500);

		Volatile.Read(ref activated).Should().Be(250); // 50 + 10*20
	}

	[Fact]
	public async Task WhenConcurrentCountQueries_ShouldNotThrow()
	{
		using var service = new ActivationService();

		for (var i = 0; i < 50; i++)
			service.Register(() => { });

		service.Start(new ActivationConfig(timeBudgetMs: 5, iterationDelayMs: 10));

		var tasks = new List<Task>();

		for (var i = 0; i < 10; i++)
		{
			tasks.Add(
				Task.Run(() =>
				{
					for (var j = 0; j < 50; j++)
					{
						_ = service.PendingCount;
						_ = service.ActivatedCount;
						_ = service.IsComplete;
						Thread.Sleep(1);
					}
				}));
		}

		var act = async () => await Task.WhenAll(tasks);

		await act.Should().NotThrowAsync();
	}
}
