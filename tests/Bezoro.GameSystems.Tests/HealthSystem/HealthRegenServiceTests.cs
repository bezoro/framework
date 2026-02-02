using System;
using System.Threading.Tasks;
using Bezoro.GameSystems.HealthSystem.Services;
using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.HealthSystem;

[TestSubject(typeof(HealthRegenService))]
public class HealthRegenServiceTests
{
	private static HealthRegenService CreateService() => new();

	private static async Task WaitUntilIdle(HealthRegenService service, int timeoutMs = 10000)
	{
		int elapsed = 0;
		while (service.ActiveCount > 0 && elapsed < timeoutMs)
		{
			await Task.Delay(50);
			elapsed += 50;
		}
	}

	public class StartRegenTests
	{
		[Fact]
		public void WithAmountPerSecond_ShouldReturnValidHandle()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			var handle = service.StartRegen(health, amountPerSecond: 10u, durationSeconds: 3f);

			handle.IsValid.Should().BeTrue();
		}

		[Fact]
		public void WithTotalAmountAndTicks_ShouldReturnValidHandle()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			var handle = service.StartRegen(health, totalAmount: 50u, ticks: 5u);

			handle.IsValid.Should().BeTrue();
		}

		[Fact]
		public void ShouldClearExistingRegens()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			var first = service.AddRegen(health, amountPerSecond: 100u, durationSeconds: 5f);
			service.IsActive(first).Should().BeTrue();

			var second = service.StartRegen(health, amountPerSecond: 10u, durationSeconds: 1f);

			service.IsActive(first).Should().BeFalse();
			service.IsActive(second).Should().BeTrue();
		}

		[Fact]
		public void ShouldReportActiveCount()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			service.StartRegen(health, amountPerSecond: 10u, durationSeconds: 5f);

			service.ActiveCount.Should().Be(1);
		}
	}

	public class AddRegenTests
	{
		[Fact]
		public void ShouldStackWithExisting()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			var first  = service.AddRegen(health, amountPerSecond: 10u, durationSeconds: 5f);
			var second = service.AddRegen(health, amountPerSecond: 20u, durationSeconds: 3f);

			service.IsActive(first).Should().BeTrue();
			service.IsActive(second).Should().BeTrue();
			service.ActiveCount.Should().Be(2);
		}

		[Fact]
		public void WithTotalAmountAndTicks_ShouldReturnValidHandle()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			var handle = service.AddRegen(health, totalAmount: 30u, ticks: 3u);

			handle.IsValid.Should().BeTrue();
			service.IsActive(handle).Should().BeTrue();
		}
	}

	public class HealthRestorationTests
	{
		[Fact]
		public async Task WhenTimerTicks_ShouldRestoreHealth()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			service.StartRegen(health, amountPerSecond: 100u, durationSeconds: 2f);

			await Task.Delay(600);

			health.Current.Should().BeGreaterThan(0u, "health should increase as the regen loop ticks");
		}

		[Fact]
		public async Task WhenDurationExpires_ShouldStopAndRestoreTotalAmount()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			service.StartRegen(health, amountPerSecond: 50u, durationSeconds: 1f);

			await WaitUntilIdle(service);

			service.ActiveCount.Should().Be(0, "regen should have finished");
			health.Current.Should().Be(50u, "total restored should equal amountPerSecond * durationSeconds");
		}

		[Fact]
		public async Task TotalAmountOverload_ShouldDivideEvenly()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			service.StartRegen(health, totalAmount: 100u, ticks: 5u);

			await WaitUntilIdle(service);

			service.ActiveCount.Should().Be(0);
			health.Current.Should().Be(100u, "total restored should equal totalAmount");
		}

		[Fact]
		public async Task WhenHealthAtMax_ShouldStillTickUntilDurationExpires()
		{
			var service = CreateService();
			var health  = new Health(100u, 95u);

			var handle = service.StartRegen(health, amountPerSecond: 50u, durationSeconds: 1f);

			await Task.Delay(500);

			// Health should be capped at max
			health.Current.Should().Be(100u);
			// Regen should still be active partway through duration
			service.IsActive(handle).Should().BeTrue();
		}

		[Fact]
		public async Task SmoothTicking_ShouldHealGradually()
		{
			var service = CreateService();
			var health  = new Health(10000u, 0u);

			service.StartRegen(health, amountPerSecond: 1000u, durationSeconds: 2f);

			// Check at 300ms — should have partial healing, not a full second's worth
			await Task.Delay(300);
			uint earlyHealth = health.Current;
			earlyHealth.Should().BeGreaterThan(0u);
			earlyHealth.Should().BeLessThan(1000u, "should not have restored a full second's worth yet");
		}
	}

	public class StopTests
	{
		[Fact]
		public async Task Stop_WithValidHandle_ShouldCancelRegen()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			var handle = service.StartRegen(health, amountPerSecond: 100u, durationSeconds: 5f);
			service.IsActive(handle).Should().BeTrue();

			service.Stop(handle).Should().BeTrue();

			// Give async loop time to observe cancellation
			await Task.Delay(100);

			service.IsActive(handle).Should().BeFalse();
			service.ActiveCount.Should().Be(0);

			uint healthAtStop = health.Current;
			await Task.Delay(300);
			health.Current.Should().Be(healthAtStop, "health should not change after stopping regen");
		}

		[Fact]
		public void Stop_WithInvalidHandle_ShouldReturnFalse()
		{
			var service = CreateService();

			service.Stop(RegenHandle.None).Should().BeFalse();
		}

		[Fact]
		public async Task StopAll_ShouldCancelAllRegensOnTarget()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			var h1 = service.AddRegen(health, amountPerSecond: 10u, durationSeconds: 5f);
			var h2 = service.AddRegen(health, amountPerSecond: 20u, durationSeconds: 5f);

			service.ActiveCount.Should().Be(2);

			service.StopAll(health);

			// Give async loops time to observe cancellation
			await Task.Delay(100);

			service.IsActive(h1).Should().BeFalse();
			service.IsActive(h2).Should().BeFalse();
			service.ActiveCount.Should().Be(0);
		}

		[Fact]
		public async Task StopAll_ShouldNotAffectOtherTargets()
		{
			var service  = CreateService();
			var health1  = new Health(1000u, 0u);
			var health2  = new Health(1000u, 0u);

			service.AddRegen(health1, amountPerSecond: 10u, durationSeconds: 5f);
			var h2 = service.AddRegen(health2, amountPerSecond: 20u, durationSeconds: 5f);

			service.StopAll(health1);

			await Task.Delay(100);

			service.IsActive(h2).Should().BeTrue();
			service.ActiveCount.Should().Be(1);
		}
	}

	public class StartRepeatingRegenTests
	{
		[Fact]
		public void ShouldReturnValidHandle()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			var handle = service.StartRepeatingRegen(health, amount: 5u, TimeSpan.FromMilliseconds(100));

			handle.IsValid.Should().BeTrue();
		}

		[Fact]
		public void ShouldReportIsActive()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			var handle = service.StartRepeatingRegen(health, amount: 5u, TimeSpan.FromMilliseconds(100));

			service.IsActive(handle).Should().BeTrue();
		}

		[Fact]
		public async Task ShouldHealOverTime()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			service.StartRepeatingRegen(health, amount: 10u, TimeSpan.FromMilliseconds(50));

			await Task.Delay(300);

			health.Current.Should().BeGreaterThan(0u, "repeating regen should restore health over time");
		}

		[Fact]
		public async Task ShouldStopWhenHandleStopped()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			var handle = service.StartRepeatingRegen(health, amount: 10u, TimeSpan.FromMilliseconds(50));

			await Task.Delay(200);
			service.Stop(handle).Should().BeTrue();

			await Task.Delay(50);
			service.IsActive(handle).Should().BeFalse();

			uint healthAtStop = health.Current;
			await Task.Delay(200);
			health.Current.Should().Be(healthAtStop, "health should not change after stopping repeating regen");
		}

		[Fact]
		public void ShouldClearExistingRegens()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			var first = service.AddRegen(health, amountPerSecond: 100u, durationSeconds: 5f);
			service.IsActive(first).Should().BeTrue();

			var second = service.StartRepeatingRegen(health, amount: 5u, TimeSpan.FromMilliseconds(100));

			service.IsActive(first).Should().BeFalse();
			service.IsActive(second).Should().BeTrue();
		}

		[Fact]
		public void WithZeroAmount_ShouldThrow()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			var act = () => service.StartRepeatingRegen(health, amount: 0u, TimeSpan.FromMilliseconds(100));

			act.Should().Throw<ArgumentOutOfRangeException>();
		}

		[Fact]
		public void WithZeroInterval_ShouldThrow()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			var act = () => service.StartRepeatingRegen(health, amount: 5u, TimeSpan.Zero);

			act.Should().Throw<ArgumentOutOfRangeException>();
		}
	}

	public class AddRepeatingRegenTests
	{
		[Fact]
		public void ShouldReturnValidHandle()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			var handle = service.AddRepeatingRegen(health, amount: 5u, TimeSpan.FromMilliseconds(100));

			handle.IsValid.Should().BeTrue();
		}

		[Fact]
		public void ShouldStackWithExistingRegens()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			var first  = service.AddRegen(health, amountPerSecond: 10u, durationSeconds: 5f);
			var second = service.AddRepeatingRegen(health, amount: 5u, TimeSpan.FromMilliseconds(100));

			service.IsActive(first).Should().BeTrue();
			service.IsActive(second).Should().BeTrue();
			service.ActiveCount.Should().Be(2);
		}

		[Fact]
		public async Task ShouldHealOverTime()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			service.AddRepeatingRegen(health, amount: 10u, TimeSpan.FromMilliseconds(50));

			await Task.Delay(300);

			health.Current.Should().BeGreaterThan(0u, "repeating regen should restore health over time");
		}

		[Fact]
		public async Task ShouldStopWhenHandleStopped()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			var handle = service.AddRepeatingRegen(health, amount: 10u, TimeSpan.FromMilliseconds(50));

			await Task.Delay(200);
			service.Stop(handle).Should().BeTrue();

			await Task.Delay(50);
			service.IsActive(handle).Should().BeFalse();
		}

		[Fact]
		public void WithZeroAmount_ShouldThrow()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			var act = () => service.AddRepeatingRegen(health, amount: 0u, TimeSpan.FromMilliseconds(100));

			act.Should().Throw<ArgumentOutOfRangeException>();
		}

		[Fact]
		public void WithZeroInterval_ShouldThrow()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			var act = () => service.AddRepeatingRegen(health, amount: 5u, TimeSpan.Zero);

			act.Should().Throw<ArgumentOutOfRangeException>();
		}
	}
}
