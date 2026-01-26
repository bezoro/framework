using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(Percent))]
public class PercentComparisonOperatorsTests
{
	[Fact]
	public void GreaterThan_WhenEqual_ShouldReturnFalse()
	{
		var a = new Percent(50);
		var b = new Percent(50);

		(a > b).Should().BeFalse();
	}

	[Fact]
	public void GreaterThan_WhenGreater_ShouldReturnTrue()
	{
		var a = new Percent(75);
		var b = new Percent(50);

		(a > b).Should().BeTrue();
	}

	[Fact]
	public void GreaterThanOrEqual_WhenEqual_ShouldReturnTrue()
	{
		var a = new Percent(50);
		var b = new Percent(50);

		(a >= b).Should().BeTrue();
	}

	[Fact]
	public void GreaterThanOrEqual_WhenGreater_ShouldReturnTrue()
	{
		var a = new Percent(75);
		var b = new Percent(50);

		(a >= b).Should().BeTrue();
	}

	[Fact]
	public void LessThan_WhenEqual_ShouldReturnFalse()
	{
		var a = new Percent(50);
		var b = new Percent(50);

		(a < b).Should().BeFalse();
	}

	[Fact]
	public void LessThan_WhenLess_ShouldReturnTrue()
	{
		var a = new Percent(25);
		var b = new Percent(50);

		(a < b).Should().BeTrue();
	}

	[Fact]
	public void LessThanOrEqual_WhenEqual_ShouldReturnTrue()
	{
		var a = new Percent(50);
		var b = new Percent(50);

		(a <= b).Should().BeTrue();
	}

	[Fact]
	public void LessThanOrEqual_WhenLess_ShouldReturnTrue()
	{
		var a = new Percent(25);
		var b = new Percent(50);

		(a <= b).Should().BeTrue();
	}
}
