using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayRemoveTests
{
	[Fact]
	public void WhenItemFound_ShouldRemoveItem()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3 };
		arr.Remove(2);

		arr.ToArray().Should().Equal(1, 3);
	}

	[Fact]
	public void WhenItemNotFound_ShouldThrow()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3 };
		var act = () => arr.Remove(4);

		act.Should().Throw<InvalidOperationException>();
	}
}
