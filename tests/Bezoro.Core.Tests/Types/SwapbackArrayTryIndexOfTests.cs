using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayTryIndexOfTests
{
	[Fact]
	public void WhenItemDoesNotExist_ShouldReturnFalseAndNullIndex()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5 };

		bool found = arr.TryIndexOf(6, out uint? index);

		found.Should().BeFalse();
		index.Should().BeNull();
	}

	[Fact]
	public void WhenItemExists_ShouldReturnTrueAndIndex()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4, 5 };

		bool found = arr.TryIndexOf(3, out uint? index);

		found.Should().BeTrue();
		index.Should().Be(2);
	}

	[Fact]
	public void WhenReferenceType_ShouldReturnTrueAndIndex()
	{
		var obj1 = new object();
		var obj2 = new object();
		var obj3 = new object();
		var arr  = new SwapbackArray<object> { obj1, obj2, obj3 };

		bool found = arr.TryIndexOf(obj2, out uint? index);

		found.Should().BeTrue();
		index.Should().Be(1);
	}
}
