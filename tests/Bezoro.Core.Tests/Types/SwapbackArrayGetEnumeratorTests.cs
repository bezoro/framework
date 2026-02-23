using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayGetEnumeratorTests
{
	[Fact]
	public void WhenArrayIsEmpty_WhenCalled_ShouldNotIterate()
	{
		var arr   = new SwapbackArray<int>();
		var count = 0;

		foreach (int _ in arr)
			count++;

		count.Should().Be(0);
	}

	[Fact]
	public void WhenCollectionModifiedDuringEnumeration_WhenCalled_ShouldThrow()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

		var act = () =>
		{
			foreach (int item in arr)
			{
				if (item == 2)
					arr.Add(99);
			}
		};

		act.Should().Throw<InvalidOperationException>();
	}
}
