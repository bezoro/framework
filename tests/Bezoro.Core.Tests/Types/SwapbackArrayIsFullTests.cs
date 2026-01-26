using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayIsFullTests
{
	[Fact]
	public void WhenArrayIsFull_ShouldReturnTrue()
	{
		var arr = new SwapbackArray<int>(10);

		for (var i = 0; i < arr.Capacity; i++) arr.Add(i);

		arr.IsFull.Should().BeTrue();
	}

	[Fact]
	public void WhenArrayIsNotFull_ShouldReturnFalse()
	{
		var arr = new SwapbackArray<int>(10);

		arr.IsFull.Should().BeFalse();
	}
}
