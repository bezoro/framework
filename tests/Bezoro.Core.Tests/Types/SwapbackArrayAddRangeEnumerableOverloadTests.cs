using System;
using System.Collections.Generic;
using System.Linq;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayAddRangeEnumerableOverloadTests
{
	[Fact]
	public void WhenEmpty_WhenCalled_ShouldNotIncrementVersion()
	{
		var  arr            = new SwapbackArray<int>();
		uint initialVersion = arr.Version;

		arr.AddRange(Enumerable.Empty<int>());

		arr.Version.Should().Be(initialVersion);
	}

	[Fact]
	public void WhenNull_WhenCalled_ShouldThrow()
	{
		var arr = new SwapbackArray<int>();

		var act = () => arr.AddRange((IEnumerable<int>)null!);

		act.Should().Throw<ArgumentNullException>().WithParameterName("collection");
	}

	[Fact]
	public void WhenValid_WhenCalled_ShouldAddAllItems()
	{
		var arr = new SwapbackArray<int>();

		arr.AddRange((IEnumerable<int>)[1, 2, 3, 4]);

		arr.ToArray().Should().Equal(1, 2, 3, 4);
	}

	[Fact]
	public void WhenValid_WhenCalled_ShouldIncrementVersionOnce()
	{
		var  arr            = new SwapbackArray<int>();
		uint initialVersion = arr.Version;

		arr.AddRange(Enumerable.Range(1, 3));

		arr.Version.Should().Be(initialVersion + 1);
	}
}
