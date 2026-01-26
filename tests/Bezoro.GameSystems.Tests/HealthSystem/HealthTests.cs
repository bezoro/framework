using Bezoro.GameSystems.HealthSystem;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.HealthSystem;

[TestSubject(typeof(Health))]
public static class HealthTests
{
	public class Constructor
	{
		[Fact]
		public void WhenCurrentExceedsMax_ShouldClampAndAddOverflowToExcess()
		{
			var health = new Health(100u, 120u, 5u);

			health.Max.Should().Be(100u);
			health.Current.Should().Be(100u);
			health.Excess.Should().Be(25u);
		}

		[Fact]
		public void WhenMaxIsZero_ShouldClampCurrentAndCarryOverflowToExcess()
		{
			var health = new Health(0u, 25u, 10u);

			health.Max.Should().Be(0u);
			health.Current.Should().Be(0u);
			health.Excess.Should().Be(35u);
		}
	}

	public class CurrentHealth
	{
		[Fact]
		public void WhenDecreasing_ShouldConsumeExcessFirst()
		{
			var health = new Health(100u, 50u, 20u);

			health.DecreaseCurrentHealthBy(10u);

			health.Current.Should().Be(50u);
			health.Excess.Should().Be(10u);
		}

		[Fact]
		public void WhenDecreasingBeyondTotal_ShouldSetCurrentAndExcessToZero()
		{
			var health = new Health(100u, 10u, 5u);

			health.DecreaseCurrentHealthBy(20u);

			health.Current.Should().Be(0u);
			health.Excess.Should().Be(0u);
		}

		[Fact]
		public void WhenDecreasingPastExcess_ShouldReduceCurrent()
		{
			var health = new Health(100u, 50u, 20u);

			health.DecreaseCurrentHealthBy(25u);

			health.Current.Should().Be(45u);
			health.Excess.Should().Be(0u);
		}

		[Fact]
		public void WhenDepletingAndRestoring_ShouldMatchExpectedValues()
		{
			var health = new Health(75u, 30u);

			health.DepleteCurrentHealth();
			health.Current.Should().Be(0u);

			health.FullyRestoreCurrentHealth();
			health.Current.Should().Be(75u);
		}

		[Fact]
		public void WhenIncreasingPastMax_ShouldFillCurrentAndAddExcess()
		{
			var health = new Health(100u, 90u);

			health.IncreaseCurrentHealthBy(20u);

			health.Current.Should().Be(100u);
			health.Excess.Should().Be(10u);
		}

		[Fact]
		public void WhenIncreasingUnderMax_ShouldIncreaseCurrentOnly()
		{
			var health = new Health(100u, 40u);

			health.IncreaseCurrentHealthBy(10u);

			health.Current.Should().Be(50u);
			health.Excess.Should().Be(0u);
		}

		[Fact]
		public void WhenRestoringPastMax_ShouldNotCreateExcess()
		{
			var health = new Health(100u, 90u, 5u);

			health.RestoreCurrentHealthBy(20u);

			health.Current.Should().Be(100u);
			health.Excess.Should().Be(5u);
		}

		[Fact]
		public void WhenSettingCurrent_ShouldClampToMax()
		{
			var health = new Health(100u, 20u);

			health.SetCurrentHealthTo(150u);

			health.Current.Should().Be(100u);
		}

		[Fact]
		public void WhenSumExceedsUIntMax_ShouldNotLoseOverflow()
		{
			uint max    = uint.MaxValue - 1u;
			var  health = new Health(max, max);

			health.IncreaseCurrentHealthBy(10u);

			health.Current.Should().Be(max);
			health.Excess.Should().Be(10u);
		}
	}

	public class ExcessHealth
	{
		[Fact]
		public void WhenClearingAndSetting_ShouldMatchExpectedValues()
		{
			var health = new Health(100u, 0u, 50u);

			health.ClearExcessHealth();
			health.Excess.Should().Be(0u);

			health.SetExcessHealthTo(15u);
			health.Excess.Should().Be(15u);
		}

		[Fact]
		public void WhenDecreasingBeyondExcess_ShouldClampToZero()
		{
			var health = new Health(100u, 50u, 10u);

			health.DecreaseExcessHealthBy(25u);

			health.Excess.Should().Be(0u);
		}

		[Fact]
		public void WhenIncreasingBeyondUIntMax_ShouldSaturate()
		{
			var health = new Health(100u, 0u, uint.MaxValue - 1u);

			health.IncreaseExcessHealthBy(10u);

			health.Excess.Should().Be(uint.MaxValue);
		}
	}

	public class MaxHealth
	{
		[Fact]
		public void WhenClampingMax_ShouldClampCurrentAndPreserveExcess()
		{
			var health = new Health(100u, 90u, 20u);

			health.SetMaxHealthTo(50u, MaxHealthUpdateMode.ClampCurrent);

			health.Max.Should().Be(50u);
			health.Current.Should().Be(50u);
			health.Excess.Should().Be(20u);
		}

		[Fact]
		public void WhenDecreasingMaxPastZero_ShouldClampCurrentToZero()
		{
			var health = new Health(100u, 80u);

			health.DecreaseMaxHealthBy(200u);

			health.Max.Should().Be(0u);
			health.Current.Should().Be(0u);
		}

		[Fact]
		public void WhenIncreasingMaxBeyondUIntMax_ShouldSaturate()
		{
			uint max    = uint.MaxValue - 1u;
			var  health = new Health(max, 10u);

			health.IncreaseMaxHealthBy(10u);

			health.Max.Should().Be(uint.MaxValue);
			health.Current.Should().Be(10u);
		}

		[Fact]
		public void WhenPreservingPercentage_ShouldRoundAwayFromZero()
		{
			var health = new Health(2u, 1u);

			health.SetMaxHealthTo(1u, MaxHealthUpdateMode.PreservePercentage);

			health.Current.Should().Be(1u);
		}

		[Fact]
		public void WhenPreservingPercentage_ShouldScaleCurrent()
		{
			var health = new Health(100u, 25u);

			health.SetMaxHealthTo(200u, MaxHealthUpdateMode.PreservePercentage);

			health.Max.Should().Be(200u);
			health.Current.Should().Be(50u);
		}

		[Fact]
		public void WhenPreservingPercentageWithZeroMax_ShouldClampCurrent()
		{
			var health = new Health(0u, 0u);

			health.SetMaxHealthTo(10u, MaxHealthUpdateMode.PreservePercentage);

			health.Max.Should().Be(10u);
			health.Current.Should().Be(0u);
		}
	}

	public class Percentage
	{
		[Fact]
		public void ShouldRoundToNearestPercent()
		{
			var health = new Health(3u, 2u);

			health.Percentage.Value.Should().Be(67);
		}

		[Fact]
		public void WhenMaxIsZero_ShouldBeZero()
		{
			var health = new Health(0u, 0u);

			health.Percentage.Value.Should().Be(0);
		}
	}
}
