using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(Percent))]
public class PercentFromTotalsTests
{
	[Fact]
	public void FromTotals_WhenMaxIsZero_ShouldReturnZero()
	{
		var percent = Percent.FromTotals((10u, 0u), (5u, 0u));

		percent.Value.Should().Be(0);
	}

	[Fact]
	public void FromTotals_WhenNeedsRounding_ShouldRoundToNearest()
	{
		var percent = Percent.FromTotals((2u, 3u));

		percent.Value.Should().Be(67);
	}

	[Fact]
	public void FromTotals_WhenCurrentExceedsMax_ShouldClampTo100()
	{
		var percent = Percent.FromTotals((120u, 100u), (10u, 0u));

		percent.Value.Should().Be(100);
	}

	[Fact]
	public void FromTotals_WhenNoPairs_ShouldReturnZero()
	{
		var percent = Percent.FromTotals();

		percent.Value.Should().Be(0);
	}

	[Fact]
	public void FromTotals_WhenMultiplePairs_ShouldSumAll()
	{
		var percent = Percent.FromTotals((10u, 20u), (15u, 30u), (5u, 10u));

		percent.Value.Should().Be(50);
	}
}
