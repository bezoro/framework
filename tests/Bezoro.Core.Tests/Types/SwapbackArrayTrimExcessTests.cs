using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayTrimExcessTests
{
	[Fact]
	public void WhenAtMinimumCapacity_WhenCalled_ShouldNotShrink()
	{
		var arr = new SwapbackArray<int> { 1 };

		arr.TrimExcess();

		arr.Capacity.Should().Be(4);
	}

	[Fact]
	public void WhenUtilizationAtOrAboveTrimThreshold_WhenCalled_ShouldNotTrim()
	{
		const uint INITIAL_CAPACITY = 100u;
		var        arr              = new SwapbackArray<int>(INITIAL_CAPACITY);
		var        threshold        = 90u;
		for (var i = 0; i < threshold; i++) arr.Add(i);

		arr.TrimExcess();

		arr.Capacity.Should().Be(INITIAL_CAPACITY);
	}

	[Fact]
	public void WhenUtilizationBelowShrinkThreshold_WhenCalled_ShouldTrim()
	{
		const uint INITIAL_CAPACITY = 100u;
		var        arr              = new SwapbackArray<int>(INITIAL_CAPACITY);
		uint       threshold        = 49;
		for (var i = 0; i < threshold; i++) arr.Add(i);

		arr.TrimExcess(Percent.Half);

		arr.Capacity.Should().Be(threshold);
	}
}
