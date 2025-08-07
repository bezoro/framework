using Bezoro.Core.Common.Extensions;
using Bezoro.Core.Common.Extensions.Collections.Arrays;
using Xunit;

namespace Bezoro.Core.Tests.Collections.ArrayHelpers;

public class ArrayExtensionsUnitTests
{
	#region IsEmpty

	[Fact]
	public void IsEmpty_WhenArrayIsNull_ReturnsFalse()
	{
		int[]? ints = null;
		Assert.False(CollectionExtensions.IsEmpty(ints));
	}

	[Fact]
	public void IsEmpty_WhenArrayIsEmpty_ReturnsTrue()
	{
		var ints = new int[0];
		Assert.True(CollectionExtensions.IsEmpty(ints));
	}

	[Fact]
	public void IsEmpty_WhenArrayIsNotEmpty_ReturnsFalse()
	{
		int[] ints = new[] { 42 };
		Assert.False(CollectionExtensions.IsEmpty(ints));
	}

	#endregion

	#region IsNotNull / IsNull

	[Fact]
	public void IsNotNull_WhenArrayIsNull_ReturnsFalse()
	{
		int[]? ints = null;
		Assert.False(GenericExtensions.IsNotNull(ints));
	}

	[Fact]
	public void IsNotNull_WhenArrayIsInstantiated_ReturnsTrue()
	{
		var ints = new int[0];
		Assert.True(GenericExtensions.IsNotNull(ints));
	}

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
		int[] ints = new[] { 1, 2, 3 };
		Assert.True(ints.IsNotNullOrEmpty());
	}

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
