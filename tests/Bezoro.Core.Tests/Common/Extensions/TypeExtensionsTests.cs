using System;
using Bezoro.Core.Common.Extensions;
using JetBrains.Annotations;
using Xunit;
using AssertionExtensions = FluentAssertions.AssertionExtensions;

namespace Bezoro.Core.Tests.Common.Extensions;

[TestSubject(typeof(TypeExtensions))]
public static class TypeExtensionsTests
{
	public class Unit
	{
		[Theory]
		[InlineData(typeof(string))]
		[InlineData(typeof(object))]
		[InlineData(typeof(Unit))]
		public void IsAnonymous_ShouldReturnFalse_ForNonAnonymousTypes(Type t) =>
			AssertionExtensions.Should(t.IsAnonymous()).BeFalse();

		[Fact]
		public void IsAnonymous_ShouldReturnTrue_ForAnonymousType()
		{
			var anon = new { A = 1, B = "x" };
			AssertionExtensions.Should(anon.GetType().IsAnonymous()).BeTrue();
		}

		[Fact]
		public void IsNull_ShouldReturnFalse_WhenTypeIsNotNull() =>
			AssertionExtensions.Should(typeof(string).IsNull()).BeFalse();

		[Fact]
		public void IsNull_ShouldReturnTrue_WhenTypeIsNull()
		{
			Type? t = null;
			// Extension methods can be invoked on null references
			AssertionExtensions.Should(t!.IsNull()).BeTrue();
		}

		[Theory]
		[InlineData(typeof(string))]
		public void IsStatic_ShouldReturnFalse_ForNonStaticClasses(Type t) =>
			AssertionExtensions.Should(t.IsStatic()).BeFalse();

		[Theory]
		[InlineData(typeof(Math))]
		public void IsStatic_ShouldReturnTrue_ForStaticClasses(Type t) =>
			AssertionExtensions.Should(t.IsStatic()).BeTrue();
	}
}
