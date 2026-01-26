using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayConstructorsIntOverloadTests
{
	[Fact]
	public void WhenLessThanMinimumCapacity_ShouldUseMinimumCapacity()
	{
		var arr = new SwapbackArray<int>(3);

		arr.Capacity.Should().Be(4);
	}

	[Fact]
	public void WhenParameterless_ShouldUseMinimumCapacity()
	{
		var arr = new SwapbackArray<int>();

		arr.Capacity.Should().Be(4);
		arr.Count.Should().Be(0);
	}

	[Fact]
	public void WhenValidCapacity_ShouldUseProvidedCapacity()
	{
		var arr = new SwapbackArray<int>(10);

		arr.Capacity.Should().Be(10);
		arr.Count.Should().Be(0);
	}
}
