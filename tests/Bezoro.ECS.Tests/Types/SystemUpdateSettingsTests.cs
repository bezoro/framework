using System;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Types;

[TestSubject(typeof(SystemUpdateSettings))]
public class SystemUpdateSettingsTests
{
	[Fact]
	public void FixedMilliseconds_WhenNegative_ShouldThrow()
	{
		var act = () => SystemUpdateSettings.FixedMilliseconds(-1);

		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void FixedMilliseconds_WhenPositive_ShouldReturnSeconds()
	{
		var settings = SystemUpdateSettings.FixedMilliseconds(1500);

		settings.IntervalSeconds.Should().BeApproximately(1.5f, 1e-6f);
	}

	[Fact]
	public void FixedTimeSpan_WhenNegative_ShouldThrow()
	{
		var act = () => SystemUpdateSettings.Fixed(TimeSpan.FromMilliseconds(-1));

		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void FixedTimeSpan_WhenPositive_ShouldReturnSeconds()
	{
		var settings = SystemUpdateSettings.Fixed(TimeSpan.FromMilliseconds(250));

		settings.IntervalSeconds.Should().BeApproximately(0.25f, 1e-6f);
	}
}
