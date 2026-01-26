using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayToArrayTests
{
	[Fact]
	public void WhenCalled_ShouldReturnArrayWithSameLengthAsArray()
	{
		int[] values = [1, 2, 3, 4];
		var   arr    = new SwapbackArray<int>(values);

		int[] arr2 = arr.ToArray();

		arr2.Length.Should().Be((int)arr.Count);
	}

	[Fact]
	public void WhenCalled_ShouldReturnArrayWithSameValuesAsArray()
	{
		int[] values = [1, 2, 3, 4];
		var   arr    = new SwapbackArray<int>(values);

		int[] arr2 = arr.ToArray();

		arr2.Should().Equal(arr);
	}

	[Fact]
	public void WhenCalled_ShouldReturnCopyOfArray()
	{
		int[] values = [1, 2, 3, 4];
		var   arr    = new SwapbackArray<int>(values);

		int[] arr2 = arr.ToArray();

		arr2.Should().NotBeSameAs(arr);
	}

	[Fact]
	public void WhenEmpty_ShouldReturnEmptyArray()
	{
		var arr = new SwapbackArray<int>();

		int[] arr2 = arr.ToArray();

		arr2.Should().BeEmpty();
	}
}
