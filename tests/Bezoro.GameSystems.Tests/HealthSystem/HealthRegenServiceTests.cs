using System;
using Bezoro.GameSystems.HealthSystem.Services;
using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.HealthSystem;

[TestSubject(typeof(HealthRegenService))]
public class HealthRegenServiceTests
{
	private static HealthRegenService CreateService() => HealthRegenService.CreateManual();

	private static void Simulate(HealthRegenService service, float seconds, float dt = 0.02f)
	{
		for (float t = 0f; t < seconds; t += dt)
			service.Update(dt);
	}

	public class StartRegenTests
	{
		[Fact]
		public void ShouldReturnValidHandle()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			var handle = service.StartRegen(health, amountPerSec: 10f, TimeSpan.FromSeconds(3));

			handle.IsValid.Should().BeTrue();
		}

		[Fact]
		public void ShouldClearExistingRegens()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			var first = service.AddRegen(health, amountPerSec: 100f, TimeSpan.FromSeconds(5));
			service.IsActive(first).Should().BeTrue();

			var second = service.StartRegen(health, amountPerSec: 10f, TimeSpan.FromSeconds(1));

			service.IsActive(first).Should().BeFalse();
			service.IsActive(second).Should().BeTrue();
		}

		[Fact]
		public void ShouldReportActiveCount()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			service.StartRegen(health, amountPerSec: 10f, TimeSpan.FromSeconds(5));

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

			var first  = service.AddRegen(health, amountPerSec: 10f, TimeSpan.FromSeconds(5));
			var second = service.AddRegen(health, amountPerSec: 20f, TimeSpan.FromSeconds(3));

			service.IsActive(first).Should().BeTrue();
			service.IsActive(second).Should().BeTrue();
			service.ActiveCount.Should().Be(2);
		}

		[Fact]
		public void ShouldReturnValidHandle()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			var handle = service.AddRegen(health, amountPerSec: 30f, TimeSpan.FromSeconds(1));

			handle.IsValid.Should().BeTrue();
			service.IsActive(handle).Should().BeTrue();
		}
	}

	public class HealthRestorationTests
	{
		[Fact]
		public void WhenTimerTicks_ShouldRestoreHealth()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			service.StartRegen(health, amountPerSec: 100f, TimeSpan.FromSeconds(2));

			Simulate(service, 0.6f);

			health.Current.Should().BeGreaterThan(0u, "health should increase as the regen loop ticks");
		}

		[Fact]
		public void WhenDurationExpires_ShouldStopAndRestoreTotalAmount()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			// 50 HP/s for 1s at default 20ms ticks = 50 total ticks, 1 HP/tick
			service.StartRegen(health, amountPerSec: 50f, TimeSpan.FromSeconds(1));

			Simulate(service, 1.1f);

			service.ActiveCount.Should().Be(0, "regen should have finished");
			health.Current.Should().Be(50u, "total restored should equal amountPerSec * durationSeconds");
		}

		[Fact]
		public void WithCustomTickFrequency_ShouldRestoreCorrectTotal()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			// 100 HP/s for 1s at 1000ms ticks = 1 tick of 100 HP
			service.StartRegen(health, amountPerSec: 100f, TimeSpan.FromSeconds(1), tickFrequencyMs: 1000);

			Simulate(service, 1.1f);

			service.ActiveCount.Should().Be(0);
			health.Current.Should().Be(100u, "1 tick * 100 HP/tick = 100 HP total");
		}

		[Fact]
		public void WhenHealthAtMax_ShouldStillTickUntilDurationExpires()
		{
			var service = CreateService();
			var health  = new Health(100u, 95u);

			var handle = service.StartRegen(health, amountPerSec: 50f, TimeSpan.FromSeconds(1));

			Simulate(service, 0.5f);

			health.Current.Should().Be(100u);
			service.IsActive(handle).Should().BeTrue();
		}

		[Fact]
		public void SmoothTicking_ShouldHealGradually()
		{
			var service = CreateService();
			var health  = new Health(10000u, 0u);

			service.StartRegen(health, amountPerSec: 1000f, TimeSpan.FromSeconds(2));

			Simulate(service, 0.3f);
			uint earlyHealth = health.Current;
			earlyHealth.Should().BeGreaterThan(0u);
			earlyHealth.Should().BeLessThan(1000u, "should not have restored a full second's worth yet");
		}
	}

	public class StopTests
	{
		[Fact]
		public void Stop_WithValidHandle_ShouldCancelRegen()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			var handle = service.StartRegen(health, amountPerSec: 100f, TimeSpan.FromSeconds(5));
			service.IsActive(handle).Should().BeTrue();

			service.Stop(handle).Should().BeTrue();

			service.IsActive(handle).Should().BeFalse();
			service.ActiveCount.Should().Be(0);

			uint healthAtStop = health.Current;
			Simulate(service, 0.3f);
			health.Current.Should().Be(healthAtStop, "health should not change after stopping regen");
		}

		[Fact]
		public void Stop_WithInvalidHandle_ShouldReturnFalse()
		{
			var service = CreateService();

			service.Stop(RegenHandle.None).Should().BeFalse();
		}

		[Fact]
		public void StopAll_ShouldCancelAllRegensOnTarget()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			var h1 = service.AddRegen(health, amountPerSec: 10f, TimeSpan.FromSeconds(5));
			var h2 = service.AddRegen(health, amountPerSec: 20f, TimeSpan.FromSeconds(5));

			service.ActiveCount.Should().Be(2);

			service.StopAll(health);

			service.IsActive(h1).Should().BeFalse();
			service.IsActive(h2).Should().BeFalse();
			service.ActiveCount.Should().Be(0);
		}

		[Fact]
		public void StopAll_ShouldNotAffectOtherTargets()
		{
			var service = CreateService();
			var health1 = new Health(1000u, 0u);
			var health2 = new Health(1000u, 0u);

			service.AddRegen(health1, amountPerSec: 10f, TimeSpan.FromSeconds(5));
			var h2 = service.AddRegen(health2, amountPerSec: 20f, TimeSpan.FromSeconds(5));

			service.StopAll(health1);

			service.IsActive(h2).Should().BeTrue();
			service.ActiveCount.Should().Be(1);
		}
	}

	public class RepeatingRegenTests
	{
		[Fact]
		public void StartRepeatingRegen_ShouldReturnValidHandle()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			var handle = service.StartRepeatingRegen(health, amountPerSec: 5f);

			handle.IsValid.Should().BeTrue();
			service.IsActive(handle).Should().BeTrue();
		}

		[Fact]
		public void StartRepeatingRegen_ShouldHealOverTime()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			service.StartRepeatingRegen(health, amountPerSec: 100f);

			Simulate(service, 0.3f);

			health.Current.Should().BeGreaterThan(0u, "repeating regen should restore health over time");
		}

		[Fact]
		public void StartRepeatingRegen_ShouldStopWhenHandleStopped()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			var handle = service.StartRepeatingRegen(health, amountPerSec: 100f);

			Simulate(service, 0.2f);
			service.Stop(handle).Should().BeTrue();

			service.IsActive(handle).Should().BeFalse();

			uint healthAtStop = health.Current;
			Simulate(service, 0.2f);
			health.Current.Should().Be(healthAtStop, "health should not change after stopping repeating regen");
		}

		[Fact]
		public void StartRepeatingRegen_ShouldClearExistingRegens()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			var first = service.AddRegen(health, amountPerSec: 100f, TimeSpan.FromSeconds(5));
			service.IsActive(first).Should().BeTrue();

			var second = service.StartRepeatingRegen(health, amountPerSec: 5f);

			service.IsActive(first).Should().BeFalse();
			service.IsActive(second).Should().BeTrue();
		}

		[Fact]
		public void AddRepeatingRegen_ShouldStackWithExisting()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			var first  = service.AddRegen(health, amountPerSec: 10f, TimeSpan.FromSeconds(5));
			var second = service.AddRepeatingRegen(health, amountPerSec: 5f);

			service.IsActive(first).Should().BeTrue();
			service.IsActive(second).Should().BeTrue();
			service.ActiveCount.Should().Be(2);
		}

		[Fact]
		public void AddRepeatingRegen_WithCustomTickFrequency_ShouldHeal()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			service.AddRepeatingRegen(health, amountPerSec: 10f, tickFrequencyMs: 100);

			Simulate(service, 0.35f);

			health.Current.Should().BeGreaterThan(0u, "repeating regen with custom tick frequency should heal");
		}
	}

	public class PrecisionTests
	{
		[Fact]
		public void FiniteRegen_ShouldDeliverExactTotal()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			// 7 HP/s for 1s — old float bug would deliver 6 instead of 7
			service.StartRegen(health, amountPerSec: 7f, TimeSpan.FromSeconds(1));

			Simulate(service, 1.1f);

			health.Current.Should().Be(7u, "finite regen must deliver exactly Round(7 * 1) = 7 HP");
		}

		[Fact]
		public void FiniteRegen_WithFractionalRate_ShouldDeliverExactTotal()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			// 3.5 HP/s for 2s = 7 HP total
			service.StartRegen(health, amountPerSec: 3.5f, TimeSpan.FromSeconds(2));

			Simulate(service, 2.1f);

			health.Current.Should().Be(7u, "finite regen must deliver exactly Round(3.5 * 2) = 7 HP");
		}

		[Fact]
		public void FiniteRegen_WithSmallRate_ShouldDeliverExactTotal()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			// 0.5 HP/s for 4s = 2 HP total
			service.StartRegen(health, amountPerSec: 0.5f, TimeSpan.FromSeconds(4));

			Simulate(service, 4.1f);

			health.Current.Should().Be(2u, "finite regen must deliver exactly Round(0.5 * 4) = 2 HP");
		}

		[Fact]
		public void FiniteRegen_WithCustomTickFrequency_ShouldDeliverExactTotal()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			// 100 HP/s for 1s at 1000ms ticks = 1 tick delivering 100 HP
			service.StartRegen(health, amountPerSec: 100f, TimeSpan.FromSeconds(1), tickFrequencyMs: 1000);

			Simulate(service, 1.1f);

			health.Current.Should().Be(100u, "finite regen must deliver exactly Round(100 * 1) = 100 HP");
		}

		[Fact]
		public void FractionalRate_ShouldAccumulateAndHeal()
		{
			var service = CreateService();
			var health  = new Health(1000u, 0u);

			// 0.5 HP/s at 1000ms ticks — accumulator reaches 1.0 after 2 ticks
			service.AddRepeatingRegen(health, amountPerSec: 0.5f, tickFrequencyMs: 1000);

			Simulate(service, 2.5f);

			health.Current.Should().BeGreaterThanOrEqualTo(1u, "accumulator should have reached 1.0 after two ticks");
		}
	}

	public class ValidationTests
	{
		[Fact]
		public void WithZeroAmountPerSec_ShouldThrow()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			var act = () => service.AddRegen(health, amountPerSec: 0f, TimeSpan.FromSeconds(1));

			act.Should().Throw<ArgumentOutOfRangeException>();
		}

		[Fact]
		public void WithNegativeAmountPerSec_ShouldThrow()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			var act = () => service.AddRegen(health, amountPerSec: -5f, TimeSpan.FromSeconds(1));

			act.Should().Throw<ArgumentOutOfRangeException>();
		}

		[Fact]
		public void WithZeroDuration_ShouldThrow()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			var act = () => service.AddRegen(health, amountPerSec: 10f, TimeSpan.Zero);

			act.Should().Throw<ArgumentOutOfRangeException>();
		}

		[Fact]
		public void WithNegativeDuration_ShouldThrow()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			var act = () => service.AddRegen(health, amountPerSec: 10f, TimeSpan.FromMilliseconds(-1));

			act.Should().Throw<ArgumentOutOfRangeException>();
		}

		[Fact]
		public void WithZeroTickFrequency_ShouldThrow()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			var act = () => service.AddRegen(health, amountPerSec: 10f, TimeSpan.FromSeconds(1), tickFrequencyMs: 0);

			act.Should().Throw<ArgumentOutOfRangeException>();
		}

		[Fact]
		public void WithNaNAmountPerSec_ShouldThrow()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			var act = () => service.AddRegen(health, amountPerSec: float.NaN, TimeSpan.FromSeconds(1));

			act.Should().Throw<ArgumentOutOfRangeException>();
		}

		[Fact]
		public void WithInfinityAmountPerSec_ShouldThrow()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			var act = () => service.AddRegen(health, amountPerSec: float.PositiveInfinity, TimeSpan.FromSeconds(1));

			act.Should().Throw<ArgumentOutOfRangeException>();
		}

		[Fact]
		public void WithNegativeInfinityAmountPerSec_ShouldThrow()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			var act = () => service.AddRepeatingRegen(health, amountPerSec: float.NegativeInfinity);

			act.Should().Throw<ArgumentOutOfRangeException>();
		}

		[Fact]
		public void RepeatingRegen_WithNaNAmountPerSec_ShouldThrow()
		{
			var service = CreateService();
			var health  = new Health(100u, 50u);

			var act = () => service.StartRepeatingRegen(health, amountPerSec: float.NaN);

			act.Should().Throw<ArgumentOutOfRangeException>();
		}

		[Fact]
		public void WithNullTarget_ShouldThrow()
		{
			var service = CreateService();

			var act = () => service.AddRegen(null!, amountPerSec: 10f, TimeSpan.FromSeconds(1));

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void RepeatingRegen_WithNullTarget_ShouldThrow()
		{
			var service = CreateService();

			var act = () => service.AddRepeatingRegen(null!, amountPerSec: 10f);

			act.Should().Throw<ArgumentNullException>();
		}
	}
}
