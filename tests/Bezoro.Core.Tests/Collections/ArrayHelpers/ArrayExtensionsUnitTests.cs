using System;
using Bezoro.Core.Common.Extensions;
using Xunit;

namespace Bezoro.Core.Tests.Collections.ArrayHelpers;

public class ArrayExtensionsUnitTests
{
#region IsEmpty

	[Fact]
	public void IsEmpty_WhenArrayIsNull_ReturnsFalse()
	{
		int[]? ints = null;
		Assert.False(ints.IsEmpty());
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
		var ints = new[] { 42 };
		Assert.False(ints.IsEmpty());
	}

#endregion

#region IsNotEmpty

	[Fact]
	public void IsNotEmpty_WhenArrayIsNull_ReturnsFalse()
	{
		string[]? strings = null;
		Assert.False(strings.IsNotEmpty());
	}

	[Fact]
	public void IsNotEmpty_WhenArrayIsEmpty_ReturnsFalse()
	{
		var strings = Array.Empty<string>();
		Assert.False(strings.IsNotEmpty());
	}

	[Fact]
	public void IsNotEmpty_WhenArrayIsNotEmpty_ReturnsTrue()
	{
		var strings = new[] { "hello" };
		Assert.True(strings.IsNotEmpty());
	}

#endregion

#region IsNotNull / IsNull

	[Fact]
	public void IsNotNull_WhenArrayIsNull_ReturnsFalse()
	{
		int[]? ints = null;
		Assert.False(ints.IsNotNull());
	}

	[Fact]
	public void IsNotNull_WhenArrayIsInstantiated_ReturnsTrue()
	{
		var ints = new int[0];
		Assert.True(ints.IsNotNull());
	}

	[Fact]
	public void IsNull_WhenArrayIsNull_ReturnsTrue()
	{
		int[]? ints = null;
		Assert.True(ints.IsNull());
	}

	[Fact]
	public void IsNull_WhenArrayIsInstantiated_ReturnsFalse()
	{
		var ints = new int[1];
		Assert.False(ints.IsNull());
	}

#endregion

#region IsNotNullOrEmpty / IsNullOrEmpty

	[Fact]
	public void IsNotNullOrEmpty_WhenArrayIsNull_ReturnsFalse()
	{
		int[]? ints = null;
		Assert.False(ints.IsNotNullOrEmpty());
	}

	[Fact]
	public void IsNotNullOrEmpty_WhenArrayIsEmpty_ReturnsFalse()
	{
		var ints = new int[0];
		Assert.False(ints.IsNotNullOrEmpty());
	}

	[Fact]
	public void IsNotNullOrEmpty_WhenArrayIsNotEmpty_ReturnsTrue()
	{
		var ints = new[] { 1, 2, 3 };
		Assert.True(ints.IsNotNullOrEmpty());
	}

	[Fact]
	public void IsNullOrEmpty_WhenArrayIsNull_ReturnsTrue()
	{
		string[]? strings = null;
		Assert.True(strings.IsNullOrEmpty());
	}

	[Fact]
	public void IsNullOrEmpty_WhenArrayIsEmpty_ReturnsTrue()
	{
		var strings = new string[0];
		Assert.True(strings.IsNullOrEmpty());
	}

	[Fact]
	public void IsNullOrEmpty_WhenArrayIsNotEmpty_ReturnsFalse()
	{
		var strings = new[] { "value" };
		Assert.False(strings.IsNullOrEmpty());
	}

#endregion
}
