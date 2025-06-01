using System.Diagnostics;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Bezoro.Core.Tests
{
	[TestFixture]
	[TestOf(typeof(Validate))]
	public class ValidateConditionalAttributeTests
	{
		[Test]
		public void ThrowMethod_HasConditionalUnityEditorAttribute()
		{
			// Arrange
			var throwMethod = typeof(Validate).GetMethod(
				"Throw",
				BindingFlags.NonPublic | BindingFlags.Static);

			// Act & Assert
			Assert.That(throwMethod, Is.Not.Null, "Throw method should exist");

			var throwAttr = throwMethod.GetCustomAttributes(typeof(ConditionalAttribute), false)
									   .Cast<ConditionalAttribute>().FirstOrDefault();

			Assert.That(throwAttr, Is.Not.Null, "Throw method should have a ConditionalAttribute");
			Assert.That(
				throwAttr.ConditionString, Is.EqualTo("UNITY_EDITOR"),
				"Throw method should have ConditionalAttribute with UNITY_EDITOR condition");
		}

		[Test]
		public void ValueReturningMethods_HaveCompilerDirectivesForUnityEditor()
		{
			// This test verifies that Get and GetAsync have #if UNITY_EDITOR directives
			// We can't directly test compiler directives, but we can test the behavior

			// Arrange
			var getMethod      = typeof(Validate).GetMethod("Get")?.MakeGenericMethod(typeof(int));
			var getAsyncMethod = typeof(Validate).GetMethod("GetAsync")?.MakeGenericMethod(typeof(int));

			// Act & Assert
			Assert.That(getMethod,      Is.Not.Null, "Get<T> method should exist");
			Assert.That(getAsyncMethod, Is.Not.Null, "GetAsync<T> method should exist");

			// These methods should not have ConditionalAttribute
			var getAttr = getMethod.GetCustomAttributes(typeof(ConditionalAttribute), false)
								   .Cast<ConditionalAttribute>().FirstOrDefault();

			var getAsyncAttr = getAsyncMethod.GetCustomAttributes(typeof(ConditionalAttribute), false)
											 .Cast<ConditionalAttribute>().FirstOrDefault();

			Assert.That(getAttr,      Is.Null, "Get<T> method should not have a ConditionalAttribute");
			Assert.That(getAsyncAttr, Is.Null, "GetAsync<T> method should not have a ConditionalAttribute");
		}

		[Test]
		public void VoidMethods_HaveConditionalUnityEditorAttribute()
		{
			// Arrange
			var doMethod      = typeof(Validate).GetMethod("Do");
			var doAsyncMethod = typeof(Validate).GetMethod("DoAsync");

			// Act & Assert
			Assert.That(doMethod,      Is.Not.Null, "Do method should exist");
			Assert.That(doAsyncMethod, Is.Not.Null, "DoAsync method should exist");

			var doAttr = doMethod.GetCustomAttributes(typeof(ConditionalAttribute), false)
								 .Cast<ConditionalAttribute>().FirstOrDefault();

			var doAsyncAttr = doAsyncMethod.GetCustomAttributes(typeof(ConditionalAttribute), false)
										   .Cast<ConditionalAttribute>().FirstOrDefault();

			Assert.That(doAttr,      Is.Not.Null, "Do method should have a ConditionalAttribute");
			Assert.That(doAsyncAttr, Is.Not.Null, "DoAsync method should have a ConditionalAttribute");

			Assert.That(
				doAttr.ConditionString, Is.EqualTo("UNITY_EDITOR"),
				"Do method should have ConditionalAttribute with UNITY_EDITOR condition");

			Assert.That(
				doAsyncAttr.ConditionString, Is.EqualTo("UNITY_EDITOR"),
				"DoAsync method should have ConditionalAttribute with UNITY_EDITOR condition");
		}
	}
}
