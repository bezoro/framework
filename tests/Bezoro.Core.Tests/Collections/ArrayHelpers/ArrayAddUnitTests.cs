using System.Linq;
using Bezoro.Core.Common.Helpers;
using Xunit;

namespace Bezoro.Core.Tests.Collections.ArrayHelpers;

public class ArrayAddUnitTests
{
	[Fact]
	public void Add_WhenArrayHasEmptySlot_FillsFirstEmptySlotWithElement()
	{
		// Arrange
		var array        = new[] { new TestObject(), null, new TestObject() };
		var elementToAdd = new TestObject();

		// Act
		ArrayHelper.Add(ref array, elementToAdd);

		// Assert
		Assert.Contains(elementToAdd, array);
		Assert.Equal(0, array.Count(obj => obj == null));
	}

	[Fact]
	public void Add_WhenArrayIsEmpty_InitializesArrayWithElement()
	{
		// Arrange
		TestObject?[] array         = { };
		var           elementToAdd  = new TestObject();
		int           initialLength = array.Length;

		// Act
		ArrayHelper.Add(ref array, elementToAdd);

		// Assert
		Assert.NotNull(array);
		Assert.NotEmpty(array);
		Assert.Contains(elementToAdd, array);
		Assert.True(array.Length > initialLength);
	}

	[Fact]
	public void Add_WhenArrayIsFullWithNoEmptySlots_ResizesAndAppendsElement()
	{
		// Arrange
		TestObject?[] array         = { new(), new() };
		var           elementToAdd  = new TestObject();
		int           initialLength = array.Length;

		// Act
		ArrayHelper.Add(ref array, elementToAdd);

		// Assert
		Assert.NotNull(array);
		Assert.NotEmpty(array);
		Assert.Contains(elementToAdd, array);
		Assert.True(array.Length > initialLength);
	}

	[Fact]
	public void Add_WhenArrayIsNull_CreatesNewArrayWithElement()
	{
		// Arrange
		TestObject?[] array        = null;
		var           elementToAdd = new TestObject();

		// Act
		ArrayHelper.Add(ref array, elementToAdd);

		// Assert
		Assert.NotNull(array);
		Assert.Single(array);
		Assert.Equal(elementToAdd, array[0]);
	}

	[Fact]
	public void Add_WhenElementIsNull_DoesNotModifyArray()
	{
		// Arrange
		var array         = new[] { new TestObject(), new TestObject() };
		int initialLength = array.Length;

		// Act
		ArrayHelper.Add(ref array, null);

		// Assert
		Assert.Equal(initialLength, array.Length);
		Assert.DoesNotContain(null, array);
	}

	[Fact]
	public void Add_WithOutputParameter_ReturnsCorrectIndexOfAddedElement()
	{
		// Arrange
		TestObject?[] array         = { new(), new(), new() };
		var           elementToAdd  = new TestObject();
		int           initialLength = array.Length;

		// Act
		ArrayHelper.Add(ref array, elementToAdd, out int index);

		// Assert
		Assert.Contains(elementToAdd, array);
		Assert.Equal(initialLength, index);
	}

	private class TestObject { }
}
