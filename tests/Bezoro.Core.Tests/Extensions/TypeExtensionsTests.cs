using System;
using Bezoro.Core.Extensions;
using JetBrains.Annotations;
using Xunit;
using AssertionExtensions = FluentAssertions.AssertionExtensions;

namespace Bezoro.Core.Tests.Extensions;

[TestSubject(typeof(TypeExtensions))]
public class TypeExtensionsTests
{
	[Theory]
	[InlineData(typeof(string))]
	[InlineData(typeof(object))]
	[InlineData(typeof(Unit))]
	public void IsAnonymous_WhenCalled_ShouldReturnFalse_ForNonAnonymousTypes(Type t) =>
		AssertionExtensions.Should(t.IsAnonymous()).BeFalse();

	[Fact]
	public void IsAnonymous_WhenCalled_ShouldReturnTrue_ForAnonymousType()
	{
		var anon = new { A = 1, B = "x" };
		AssertionExtensions.Should(anon.GetType().IsAnonymous()).BeTrue();
	}

	[Fact]
	public void IsNull_WhenCalled_ShouldReturnFalse_WhenTypeIsNotNull() =>
		AssertionExtensions.Should(typeof(string).IsNull()).BeFalse();

	[Fact]
	public void IsNull_WhenCalled_ShouldReturnTrue_WhenTypeIsNull()
	{
		Type? t = null;
		// Extension methods can be invoked on null references
		AssertionExtensions.Should(t!.IsNull()).BeTrue();
	}

	[Theory]
	[InlineData(typeof(string))]
	public void IsStatic_WhenCalled_ShouldReturnFalse_ForNonStaticClasses(Type t) =>
		AssertionExtensions.Should(t.IsStatic()).BeFalse();

	[Theory]
	[InlineData(typeof(Math))]
	public void IsStatic_WhenCalled_ShouldReturnTrue_ForStaticClasses(Type t) =>
		AssertionExtensions.Should(t.IsStatic()).BeTrue();
}

internal sealed class Unit;
