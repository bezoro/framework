using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayAddTests
{
	[Fact]
	public void WhenArrayIsFull_WhenCalled_ShouldDoubleCapacity()
	{
		const uint INITIAL_CAPACITY = 5u;
		var        arr              = new SwapbackArray<int>(INITIAL_CAPACITY);

		for (var i = 0; i < INITIAL_CAPACITY + 1; i++)
			arr.Add(i);

		arr.Capacity.Should().Be(INITIAL_CAPACITY * 2);
	}

	[Fact]
	public void WhenSuccessful_WhenCalled_ShouldAppendItem()
	{
		int[] startingValues = [1, 2, 3];
		// ReSharper disable once UseObjectOrCollectionInitializer
		var arr = new SwapbackArray<int>(startingValues);

		arr.Add(4);

		arr.ToArray().Should().Equal(1, 2, 3, 4);
		arr.Count.Should().Be(4);
	}

	[Fact]
	public void WhenSuccessful_WhenCalled_ShouldIncrementVersion()
	{
		var  arr            = new SwapbackArray<int>();
		uint initialVersion = arr.Version;

		arr.Add(1);

		arr.Version.Should().Be(initialVersion + 1);
	}
}
