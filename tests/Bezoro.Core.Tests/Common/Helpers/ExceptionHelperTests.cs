using System;
using System.Reflection;
using Bezoro.Core.Common.Helpers;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Helpers;

[TestSubject(typeof(ExceptionHelper))]
public static class ExceptionHelperTests
{
	private sealed class Dummy;

	public class Unit
	{
		[Fact]
		public void ThrowException_ShouldComposeMessage_WithAllDetails()
		{
			var instance = new Dummy();

			var act = () => ExceptionHelper.ThrowException<InvalidOperationException>(
				instance,
				"DoWork",
				"Something broke");

			var ex = act.Should().Throw<InvalidOperationException>().Which;
			ex.Message.Should().Be("InvalidOperationException occurred in Dummy.DoWork: Something broke");
		}

		[Fact]
		public void ThrowException_ShouldComposeMessage_WithDefaults_WhenArgsAreNullOrWhitespace()
		{
			var act = () => ExceptionHelper.ThrowException<InvalidOperationException>(
				null,
				"   ");

			var ex = act.Should().Throw<InvalidOperationException>().Which;
			ex.Message.Should().Be("InvalidOperationException occurred in Unknown");
		}

		[Fact]
		public void FormatExceptionMessage_ShouldIncludeParamTypes_WhenProvided_ViaReflection()
		{
			var method = typeof(ExceptionHelper).GetMethod(
				"FormatExceptionMessage",
				BindingFlags.NonPublic | BindingFlags.Static);

			method.Should().NotBeNull();

			object? instance = new Dummy();
			var parameters = new[]
			{
				"CustomException",                                   // exceptionType
				instance,                                            // objectInstance
				"Run",                                               // methodName
				"Oops",                                              // message
				new object?[] { 1, "abc", null, DateTime.UnixEpoch } // paramNames (params object[])
			};

			// Invoke and assert
			var result = (string)method!.Invoke(null, parameters)!;

			result.Should().Be(
				"CustomException occurred in Dummy.Run for parameters [Int32, String, Unknown, DateTime]: Oops");
		}
	}
}
