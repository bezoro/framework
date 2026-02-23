using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayClearTests
{
	[Fact]
	public void WhenSuccessful_WhenCalled_ShouldIncrementVersion()
	{
		var  arr            = new SwapbackArray<int> { 1, 2, 3 };
		uint initialVersion = arr.Version;

		arr.Clear();

		arr.Version.Should().Be(initialVersion + 1);
	}

	[Fact]
	public void WhenSuccessful_WhenCalled_ShouldResetCountToZero()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3 };

		arr.Clear();

		arr.Count.Should().Be(0);
		arr.IsEmpty.Should().BeTrue();
	}

	[Fact]
	public void WhenSuccessful_WhenWithoutTrim_ShouldMaintainCapacity()
	{
		var arr = new SwapbackArray<int>(100);
		for (var i = 0; i < 50; i++) arr.Add(i);
		uint capacity = arr.Capacity;

		arr.Clear(false);

		arr.Capacity.Should().Be(capacity);
	}

	[Fact]
	public void WhenSuccessful_WhenWithTrim_ShouldShrinkToMinimumCapacity()
	{
		var arr = new SwapbackArray<int>(100);
		for (var i = 0; i < 50; i++) arr.Add(i);

		arr.Clear();

		arr.Capacity.Should().Be(arr.MinimumArraySize);
	}
}
