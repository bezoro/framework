using System;
using System.Collections.Generic;
using Bezoro.Core.Helpers;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Helpers;

[TestSubject(typeof(ValidationHelper))]
public class ValidationHelperTests
{
	[Fact]
	public void Condition_WhenCalled_ShouldNotThrow_WhenConditionIsTrue()
	{
		var act = () => ValidationHelper.Condition(true, "ok", new Dummy(), "Run");
		act.Should().NotThrow();
	}

	[Fact]
	public void Condition_WhenCalled_ShouldThrow_WithComposedMessage_WhenConditionIsFalse()
	{
		var instance = new Dummy();

		var act = () => ValidationHelper.Condition(false, "failure", instance, "DoWork");

		var ex = act.Should().Throw<InvalidOperationException>().Which;
		ex.Message.Should().Be("InvalidOperationException occurred in Dummy.DoWork: failure");
	}

	[Fact]
	public void IsFalse_WhenDefault_ShouldNotThrow_WhenConditionIsFalse()
	{
		var act = () => ValidationHelper.IsFalse(false, "ignored");
		act.Should().NotThrow();
	}

	[Fact]
	public void IsFalse_WhenDefault_ShouldThrow_WhenConditionIsTrue()
	{
		var act = () => ValidationHelper.IsFalse(true, "boom");
		act.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void IsFalse_WhenGeneric_ShouldNotThrow_WhenConditionIsFalse()
	{
		var act = () => ValidationHelper.IsFalse<ArgumentException>(false, "ignored");
		act.Should().NotThrow();
	}

	[Fact]
	public void IsFalse_WhenGeneric_ShouldThrowSpecificException_WhenConditionIsTrue()
	{
		var act = () => ValidationHelper.IsFalse<ArgumentException>(true, "bad");
		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void IsNotNull_WhenCalled_ShouldNotThrow_WhenObjectIsNotNull()
	{
		var act = () => ValidationHelper.IsNotNull("x");
		act.Should().NotThrow();
	}

	[Fact]
	public void IsNotNull_WhenCalled_ShouldThrow_WhenObjectIsNull()
	{
		string? obj = null;
		var     act = () => ValidationHelper.IsNotNull(obj!);
		act.Should().Throw<ArgumentNullException>().WithParameterName("obj");
	}

	[Fact]
	public void IsPositiveValue_WhenCalled_ShouldNotThrow_WhenValueIsPositive()
	{
		var act = () => ValidationHelper.IsPositiveValue(0.01f);
		act.Should().NotThrow();
	}

	[Theory]
	[InlineData(0f)]
	[InlineData(-1f)]
	public void IsPositiveValue_WhenCalled_ShouldThrow_WhenValueIsNonPositive(float value)
	{
		var act = () => ValidationHelper.IsPositiveValue(value, "amount");
		var ex  = act.Should().Throw<ArgumentException>().Which;
		ex.Message.Should().Contain("ArgumentException occurred");
		ex.Message.Should().Contain("amount must be positive. Received:");
	}

	[Fact]
	public void IsSubclassOf_WhenCalled_ShouldNotThrow_WhenTypeIsSubclass()
	{
		var act = () => ValidationHelper.IsSubclassOf<BaseType>(new Dummy(), "Check", typeof(DerivedType));
		act.Should().NotThrow();
	}

	[Fact]
	public void IsSubclassOf_WhenCalled_ShouldThrow_WhenTypeIsNotSubclass()
	{
		var act = () => ValidationHelper.IsSubclassOf<BaseType>(new Dummy(), "Check", typeof(string));
		var ex  = act.Should().Throw<ArgumentException>().Which;
		ex.Message.Should().Contain("Type System.String is not a subclass of BaseType");
	}

	[Theory]
	[InlineData(0, 0, 0, 7)]
	[InlineData(7, 7, 0, 7)]
	[InlineData(3, 4, 0, 7)]
	public void IsWithinRange_WhenCalled_ShouldNotThrow_WhenInsideOrOnBoundary(int file, int rank, int min, int max)
	{
		var act = () => ValidationHelper.IsWithinRange(file, rank, min, max);
		act.Should().NotThrow();
	}

	[Theory]
	[InlineData(-1, 0,  0, 7)]
	[InlineData(0,  -1, 0, 7)]
	[InlineData(8,  0,  0, 7)]
	[InlineData(0,  8,  0, 7)]
	public void IsWithinRange_WhenCalled_ShouldThrow_WhenOutside(int file, int rank, int min, int max)
	{
		var act = () => ValidationHelper.IsWithinRange(file, rank, min, max);
		var ex  = act.Should().Throw<ArgumentOutOfRangeException>().Which;

		// Single-string ctor sets ParamName to the provided string; check that.
		var expectedParam = $"Coordinates must be between {min} and {max}. Received: file={file}, rank={rank}";
		ex.ParamName.Should().Be(expectedParam);
	}

	[Fact]
	public void ListNotNullOrEmpty_WhenCalled_ShouldNotThrow_WhenListHasItems()
	{
		var list = new List<int> { 1 };
		var act  = () => ValidationHelper.ListNotNullOrEmpty(list);
		act.Should().NotThrow();
	}

	[Fact]
	public void ListNotNullOrEmpty_WhenCalled_ShouldThrow_WhenListIsEmpty()
	{
		var list = new List<int>();
		var act  = () => ValidationHelper.ListNotNullOrEmpty(list);
		var ex   = act.Should().Throw<ArgumentException>().Which;
		ex.Message.Should().Contain("list is null or empty");
	}

	[Fact]
	public void ListNotNullOrEmpty_WhenCalled_ShouldThrow_WhenListIsNull()
	{
		List<int> list = null!;
		var       act  = () => ValidationHelper.ListNotNullOrEmpty(list);
		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void String_WhenCalled_ShouldNotThrow_WhenValid()
	{
		var act = () => ValidationHelper.String("ok");
		act.Should().NotThrow();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void String_WhenCalled_ShouldThrow_WhenNullOrWhitespace(string? value)
	{
		var act = () => ValidationHelper.String(value!);
		var ex  = act.Should().Throw<ArgumentException>().Which;
		ex.Message.Should().Contain("value is null or empty");
	}

	[Fact]
	public void ThrowIfObjectIsNull_WhenCalled_ShouldStillThrow_WhenObjectIsNotNull_PerCurrentBehavior()
	{
		object obj = new();
		var    act = () => ValidationHelper.ThrowIfObjectIsNull(obj, "thing", "extra info");
		var    ex  = act.Should().Throw<ArgumentNullException>().Which;
		ex.Message.Should().Contain("ArgumentNullException occurred");
		ex.Message.Should().Contain("thing is null; extra info");
	}

	[Fact]
	public void ThrowIfObjectIsNull_WhenCalled_ShouldThrow_WhenObjectIsNull()
	{
		object obj = null!;
		var    act = () => ValidationHelper.ThrowIfObjectIsNull(obj);
		var    ex  = act.Should().Throw<ArgumentNullException>().Which;
		// CallerArgumentExpression captures the expression at the call site inside ValidationHelper
		ex.ParamName.Should().Be("objectToValidate");
	}

	[Fact]
	public void ValueNotAboveMax_WhenCalled_ShouldNotThrow_WhenValueIsEqualOrLess()
	{
		var a1 = () => ValidationHelper.ValueNotAboveMax(5, 5);
		var a2 = () => ValidationHelper.ValueNotAboveMax(4, 5);
		a1.Should().NotThrow();
		a2.Should().NotThrow();
	}

	[Fact]
	public void ValueNotAboveMax_WhenCalled_ShouldThrow_WhenValueIsGreater()
	{
		var act = () => ValidationHelper.ValueNotAboveMax(6, 5, "count", "limit");
		var ex  = act.Should().Throw<ArgumentException>().Which;
		ex.Message.Should().Contain("count cannot be greater than limit. Received: 6, Max: 5");
	}

	internal class BaseType;

	internal sealed class DerivedType : BaseType;

	internal sealed class Dummy;
}
