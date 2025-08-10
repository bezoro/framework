using System;
using Bezoro.Core.Common.Extensions;
using Bezoro.Core.Common.Extensions.Collections;
using Bezoro.Core.Common.Extensions.Collections.Arrays;
using Xunit;

namespace Bezoro.Core.Tests.Collections.ArrayHelpers;

public class ArrayExtensionsUnitTests
{
	#region IsEmpty

	[Fact]
	public void IsEmpty_WhenArrayIsNull_Throws()
	{
		int[]? ints = null;
		Assert.ThrowsAny<Exception>(() => ints.IsEmpty());
	}

	[Fact]
	public void IsEmpty_WhenArrayIsEmpty_ReturnsTrue()
	{
		var ints = new int[0];
		Assert.True(ints.IsEmpty());
	}

	[Fact]
	public void IsEmpty_WhenArrayIsNotEmpty_ReturnsFalse()
	{
		int[] ints = new[] { 42 };
		Assert.False(ints.IsEmpty());
	}

	#endregion

	#region IsNull

	[Fact]
	public void IsNull_WhenArrayIsNull_ReturnsTrue()
	{
		int[]? ints = null;
		Assert.True(GenericExtensions.IsNull(ints));
	}

	[Fact]
	public void IsNull_WhenArrayIsInstantiated_ReturnsFalse()
	{
		var ints = new int[1];
		Assert.False(GenericExtensions.IsNull(ints));
	}

	#endregion

	#region IsNullOrEmpty

	[Fact]
	public void IsNullOrEmpty_WhenArrayIsNull_ReturnsTrue()
	{
		string[]? strings = null;
		Assert.True(CollectionExtensions.IsNullOrEmpty(strings));
	}

	[Fact]
	public void IsNullOrEmpty_WhenArrayIsEmpty_ReturnsTrue()
	{
		var strings = new string[0];
		Assert.True(CollectionExtensions.IsNullOrEmpty(strings));
	}

	[Fact]
	public void IsNullOrEmpty_WhenArrayIsNotEmpty_ReturnsFalse()
	{
		string[] strings = new[] { "value" };
		Assert.False(CollectionExtensions.IsNullOrEmpty(strings));
	}

	#endregion
}
