using System;
using System.Collections.Generic;
using System.Linq;
using Bezoro.Core.Types;
using Xunit;

namespace Bezoro.Core.Tests.Types;

public class SwapbackArraySafetyTests
{
	[Theory]
	[InlineData(0)]
	[InlineData(3)]
	[InlineData(4)]
	public void ReplaceAt_RejectsCountWithoutMutatingCollection(int count)
	{
		var array = new SwapbackArray<int>();
		for (var i = 0; i < count; i++) array.Add(i);
		uint version = array.Version;

		Assert.Throws<ArgumentOutOfRangeException>(() => array.ReplaceAt(array.Count, 99));
		Assert.Equal(version, array.Version);
		Assert.Equal((uint)count, array.Count);
	}

	[Fact]
	public void AddRange_CanAppendItselfOnce()
	{
		var array = new SwapbackArray<int> { 1, 2, 3 };
		uint version = array.Version;
		array.AddRange(array);
		Assert.Equal(new[] { 1, 2, 3, 1, 2, 3 }, array.ToArray());
		Assert.Equal(version + 1, array.Version);
	}

	[Fact]
	public void AddRange_PartialEnumerationFailureInvalidatesExistingEnumerator()
	{
		var array = new SwapbackArray<int> { 1 };
		var enumerator = array.GetEnumerator();
		Assert.Throws<InvalidOperationException>(() => array.AddRange(ThrowAfterOne()));
		Assert.Equal(new[] { 1, 2 }, array.ToArray());
		Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
	}

	[Fact]
	public void AddRange_AliasedQueryFailsBeforeUnboundedGrowth()
	{
		var array = new SwapbackArray<int> { 1, 2, 3 };
		Assert.Throws<InvalidOperationException>(() => array.AddRange(array.Where(_ => true)));
		Assert.Equal(new[] { 1, 2, 3, 1 }, array.ToArray());
	}

	private static IEnumerable<int> ThrowAfterOne()
	{
		yield return 2;
		throw new InvalidOperationException("Enumeration failed.");
	}
}
