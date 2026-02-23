using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(Percent))]
public class PercentToRatioTests
{
	[Fact]
	public void WhenCalled_WhenCalled_ShouldReturnRatio()
	{
		var p = new Percent(10);

		float r = p.ToRatio();

		r.Should().Be(0.1f);
	}

	[Theory]
	[InlineData(0,   0.0f)]
	[InlineData(25,  0.25f)]
	[InlineData(50,  0.5f)]
	[InlineData(75,  0.75f)]
	[InlineData(100, 1.0f)]
	public void WhenCalledWithValue_WhenCalled_ShouldReturnExpectedRatio(byte value, float expectedRatio)
	{
		var p = new Percent(value);

		p.ToRatio().Should().Be(expectedRatio);
	}

	[Fact]
	public void WhenFull_WhenCalled_ShouldReturnOne()
	{
		float r = Percent.Full.ToRatio();

		r.Should().Be(1.0f);
	}

	[Fact]
	public void WhenZero_WhenCalled_ShouldReturnZero()
	{
		float r = Percent.Zero.ToRatio();

		r.Should().Be(0.0f);
	}
}
