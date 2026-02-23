using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayCopyToSpanOverloadTests
{
	[Fact]
	public void WhenDestinationIsTooSmall_WhenCalled_ShouldThrow()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

		var act = () => arr.CopyTo(new Span<int>(new int[2]));

		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void WhenDestinationIsValid_WhenCalled_ShouldCopyAllItems()
	{
		int[] values      = [1, 2, 3, 4];
		var   arr         = new SwapbackArray<int>(values);
		var   destination = new Span<int>(new int[4]);

		arr.CopyTo(destination);

		arr.ToArray().Should().Equal(values);
	}
}
