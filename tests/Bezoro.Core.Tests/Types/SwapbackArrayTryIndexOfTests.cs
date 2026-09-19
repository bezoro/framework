using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayTryIndexOfTests
{
	[Fact]
	public void TryGetIndex_WhenItemDoesNotExist_ShouldReturnFalseAndZeroIndex()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5 };

		bool found = arr.TryGetIndex(6, out uint index);

		found.Should().BeFalse();
		index.Should().Be(0);
	}

	[Fact]
	public void TryGetIndex_WhenItemExists_ShouldReturnTrueAndIndex()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5 };

		bool found = arr.TryGetIndex(3, out uint index);

		found.Should().BeTrue();
		index.Should().Be(2);
	}

	[Fact]
	public void TryGetIndex_WhenReferenceTypeExists_ShouldReturnTrueAndIndex()
	{
		var obj1 = new object();
		var obj2 = new object();
		var obj3 = new object();
		var arr  = new SwapbackArray<object> { obj1, obj2, obj3 };

		bool found = arr.TryGetIndex(obj2, out uint index);

		found.Should().BeTrue();
		index.Should().Be(1);
	}

	[Fact]
	public void TryIndexOf_WhenItemDoesNotExist_ShouldForwardWithNullIndex()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5 };

#pragma warning disable CS0618
		bool found = arr.TryIndexOf(6, out uint? index);
#pragma warning restore CS0618

		found.Should().BeFalse();
		index.Should().BeNull();
	}
}
