using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.GameSystems.ActivationSystem.Services;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.ActivationSystem;

[TestSubject(typeof(ActivationService))]
public class ActivationServiceActivationTests
{
	[Fact]
	public async Task WhenCallbackContextProvided_ShouldMarshalCallbacks()
	{
		using var service          = new ActivationService();
		int       contextThreadId  = -1;
		int       callbackThreadId = -1;
		var       syncContext      = new TestSynchronizationContext();

		contextThreadId = syncContext.ThreadId;

		service.Register(() => callbackThreadId = Thread.CurrentThread.ManagedThreadId);

		service.Start(
			new(
				10,
				10,
				callbackContext: syncContext
			)
		);

		await Task.Delay(300);
		syncContext.ExecutePending();

		callbackThreadId.Should().Be(Thread.CurrentThread.ManagedThreadId);
	}

	[Fact]
	public async Task WhenCompleted_ShouldRaiseCompletedEvent()
	{
		using var service         = new ActivationService();
		var       completedRaised = 0;

		service.Completed += () => Interlocked.Increment(ref completedRaised);

		service.Register(() => { });
		service.Start(new(10, 10));
		await Task.Delay(300);

		Volatile.Read(ref completedRaised).Should().BeGreaterThan(0);
	}

	[Fact]
	public async Task WhenHigherPriority_ShouldActivateFirst()
	{
		using var service = new ActivationService();
		var       order   = new ConcurrentQueue<string>();

		service.Register(() => order.Enqueue("low"));
		service.Register(() => order.Enqueue("high"),   10);
		service.Register(() => order.Enqueue("medium"), 5);

		// Use small budget to force one item per iteration
		service.Start(new(0.001, 10, 1, 1));
		await Task.Delay(300);

		string[] items = order.ToArray();
		items.Should().HaveCount(3);
		items[0].Should().Be("high");
		items[1].Should().Be("medium");
		items[2].Should().Be("low");
	}

	[Fact]
	public async Task WhenItemsRegisteredAfterStart_ShouldBePickedUp()
	{
		using var service   = new ActivationService();
		var       activated = 0;

		service.Start(new(10, 10));
		await Task.Delay(50);

		service.Register(() => Interlocked.Increment(ref activated));
		service.Register(() => Interlocked.Increment(ref activated));
		await Task.Delay(300);

		Volatile.Read(ref activated).Should().Be(2);
	}

	[Fact]
	public async Task WhenServiceStopped_ShouldNotActivateRemaining()
	{
		using var service   = new ActivationService();
		var       activated = 0;

		for (var i = 0; i < 100; i++)
			service.Register(() => Interlocked.Increment(ref activated));

		// Very slow processing: 1 item per iteration
		service.Start(new(0.001, 50, 1, 1));
		await Task.Delay(100);
		service.Stop();

		int count = Volatile.Read(ref activated);
		count.Should().BeGreaterThan(0);
		count.Should().BeLessThan(100);
	}

	[Fact]
	public async Task WhenStarted_ShouldActivateRegisteredItems()
	{
		using var service   = new ActivationService();
		var       activated = 0;

		service.Register(() => Interlocked.Increment(ref activated));
		service.Register(() => Interlocked.Increment(ref activated));
		service.Register(() => Interlocked.Increment(ref activated));

		service.Start(new(10, 10));
		await Task.Delay(300);

		Volatile.Read(ref activated).Should().Be(3);
		service.ActivatedCount.Should().Be(3);
		service.PendingCount.Should().Be(0);
	}

	private sealed class TestSynchronizationContext : SynchronizationContext
	{
		private readonly ConcurrentQueue<(SendOrPostCallback, object?)> _queue = new();

		public int ThreadId { get; } = Thread.CurrentThread.ManagedThreadId;

		public override void Post(SendOrPostCallback d, object? state)
		{
			_queue.Enqueue((d, state));
		}

		public void ExecutePending()
		{
			while (_queue.TryDequeue(out var item)) item.Item1(item.Item2);
		}
	}
}
