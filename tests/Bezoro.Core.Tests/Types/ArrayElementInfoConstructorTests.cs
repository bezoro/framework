using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(ArrayElementInfo<>))]
public class ArrayElementInfoConstructorTests
{
	[Fact]
	public void WhenArrayLengthZeroAndIndexNull_ShouldSucceed()
	{
		var info = new ArrayElementInfo<int>(null, 0, 0);

		info.Index.Should().BeNull();
		info.Element.Should().Be(0);
		info.ArrayLength.Should().Be(0);
		info.IsFound.Should().BeFalse();
	}

	[Fact]
	public void WhenArrayLengthZeroAndIndexZero_ShouldThrow()
	{
		var act = () => new ArrayElementInfo<int>(0, 42, 0);

		act.Should().Throw<ArgumentOutOfRangeException>()
		   .WithParameterName("index");
	}

	[Fact]
	public void WhenFoundElementIsNull_ShouldPreserveNull()
	{
		var info = new ArrayElementInfo<string>(2, null, 5);

		info.Index.Should().Be(2);
		info.Element.Should().BeNull();
		info.IsFound.Should().BeTrue();
	}

	[Fact]
	public void WhenIndexEqualsArrayLength_ShouldThrow()
	{
		var act = () => new ArrayElementInfo<int>(5, 42, 5);

		act.Should().Throw<ArgumentOutOfRangeException>()
		   .WithParameterName("index");
	}

	[Fact]
	public void WhenIndexGreaterThanArrayLength_ShouldThrow()
	{
		var act = () => new ArrayElementInfo<string>(10, "x", 3);

		act.Should().Throw<ArgumentOutOfRangeException>()
		   .WithParameterName("index");
	}

	[Fact]
	public void WhenIndexIsMaxValueMinusOne_ShouldSucceed()
	{
		var info = new ArrayElementInfo<int>(uint.MaxValue - 1, 42, uint.MaxValue);

		info.Index.Should().Be(uint.MaxValue - 1);
		info.ArrayLength.Should().Be(uint.MaxValue);
		info.IsFound.Should().BeTrue();
	}

	[Fact]
	public void WhenReferenceType_ShouldSetProperties()
	{
		var info = new ArrayElementInfo<string>(2, "x", 3);

		info.Index.Should().Be(2);
		info.Element.Should().Be("x");
		info.ArrayLength.Should().Be(3);
		info.IsFound.Should().BeTrue();
		ArrayElementInfo<string>.ElementType.Should().Be<string>();
		info.RuntimeElementType.Should().Be<string>();
	}

	[Fact]
	public void WhenValueType_ShouldSetProperties()
	{
		var info = new ArrayElementInfo<int>(0, 42, 10);

		info.Index.Should().Be(0);
		info.Element.Should().Be(42);
		info.ArrayLength.Should().Be(10);
		info.IsFound.Should().BeTrue();
		ArrayElementInfo<int>.ElementType.Should().Be<int>();
		info.RuntimeElementType.Should().Be<int>();
	}
}
