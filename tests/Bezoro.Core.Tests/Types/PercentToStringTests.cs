using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(Percent))]
public class PercentToStringTests
{
	[Fact]
	public void WhenCalled_WhenCalled_ShouldReturnPercentString()
	{
		var p = new Percent(10);

		p.ToString().Should().Be("10%");
	}

	[Fact]
	public void WhenFull_WhenCalled_ShouldReturn100Percent()
	{
		Percent.Full.ToString().Should().Be("100%");
	}

	[Fact]
	public void WhenZero_WhenCalled_ShouldReturnZeroPercent()
	{
		Percent.Zero.ToString().Should().Be("0%");
	}
}
