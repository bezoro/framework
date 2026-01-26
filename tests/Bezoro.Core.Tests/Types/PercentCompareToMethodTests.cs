using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(Percent))]
public class PercentCompareToMethodTests
{
	[Fact]
	public void WhenEqual_ShouldReturnZero()
	{
		var a = new Percent(50);
		var b = new Percent(50);

		a.CompareTo(b).Should().Be(0);
	}

	[Fact]
	public void WhenGreater_ShouldReturnPositive()
	{
		var a = new Percent(75);
		var b = new Percent(50);

		a.CompareTo(b).Should().BePositive();
	}

	[Fact]
	public void WhenLess_ShouldReturnNegative()
	{
		var a = new Percent(25);
		var b = new Percent(50);

		a.CompareTo(b).Should().BeNegative();
	}
}
