using System;
using System.Reflection;
using Bezoro.Core.Helpers;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Helpers;

#pragma warning disable CS0618

[TestSubject(typeof(ExceptionHelper))]
public class ExceptionHelperTests
{
	[Fact]
	public void ExceptionHelper_WhenObsolete_ShouldExposeExpectedMessage()
	{
		var attribute = typeof(ExceptionHelper).GetCustomAttribute<ObsoleteAttribute>();
		attribute.Should().NotBeNull();
		attribute!.Message
			.Should().Be("Use direct exception construction instead.");
	}

	[Fact]
	public void FormatExceptionMessage_WhenCalled_ShouldIncludeParamTypes_WhenProvided_ViaReflection()
	{
		var method = typeof(ExceptionHelper).GetMethod(
			"FormatExceptionMessage",
			BindingFlags.NonPublic | BindingFlags.Static
		);

		method.Should().NotBeNull();

		object? instance = new Dummy();
		object[] parameters = new[]
		{
			"CustomException",                                   // exceptionType
			instance,                                            // objectInstance
			"Run",                                               // methodName
			"Oops",                                              // message
			new object?[] { 1, "abc", null, DateTime.UnixEpoch } // paramNames (params object[])
		};

		// Invoke and assert
		var result = (string)method.Invoke(null, parameters)!;

		result.Should().Be(
			"CustomException occurred in Dummy.Run for parameters [Int32, String, Unknown, DateTime]: Oops"
		);
	}

	[Fact]
	public void ThrowException_WhenCalled_ShouldComposeMessage_WithAllDetails()
	{
		var instance = new Dummy();

		var act = () => ExceptionHelper.ThrowException<InvalidOperationException>(
			instance,
			"DoWork",
			"Something broke"
		);

		var ex = act.Should().Throw<InvalidOperationException>().Which;
		ex.Message.Should().Be("InvalidOperationException occurred in Dummy.DoWork: Something broke");
	}

	[Fact]
	public void ThrowException_WhenCalled_ShouldComposeMessage_WithDefaults_WhenArgsAreNullOrWhitespace()
	{
		var act = () => ExceptionHelper.ThrowException<InvalidOperationException>(
			null,
			"   "
		);

		var ex = act.Should().Throw<InvalidOperationException>().Which;
		ex.Message.Should().Be("InvalidOperationException occurred in Unknown");
	}
}

#pragma warning restore CS0618

internal sealed class Dummy;
