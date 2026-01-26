using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayIndexOfTests
{
	[Fact]
	public void WhenArrayIsEmpty_ShouldThrow()
	{
		var arr = new SwapbackArray<int>();

		var act = () => arr.IndexOf(1);

		act.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void WhenArrayIsNotEmpty_ShouldReturnIndex()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

		arr.IndexOf(2).Should().Be(1);
	}

	[Fact]
	public void WhenReferenceType_ShouldReturnIndex()
	{
		var obj1 = new object();
		var obj2 = new object();
		var obj3 = new object();
		var arr  = new SwapbackArray<object> { obj1, obj2, obj3 };

		arr.IndexOf(obj2).Should().Be(1);
	}
}
