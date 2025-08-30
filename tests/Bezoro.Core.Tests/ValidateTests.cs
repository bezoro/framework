using System;
using System.Threading.Tasks;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests;

[TestSubject(typeof(Validate))]
public static class ValidateTests
{
	public class Unit
	{
		[Fact]
		public async Task DoAsync_WithNullAction_ThrowsArgumentNullException()
		{
			var action = () => Validate.DoAsync(null!);
			await action.Should().ThrowAsync<ArgumentNullException>();
		}

		[Fact]
		public async Task DoAsync_WithValidAction_ExecutesSuccessfully()
		{
			var executed = false;
			await Validate.DoAsync(() =>
			{
				executed = true;
				return Task.CompletedTask;
			});

			executed.Should().BeTrue();
		}

		[Fact]
		public async Task GetAsync_WithNullFunction_ThrowsArgumentNullException()
		{
			var action = () => Validate.GetAsync<int>(null!);
			await action.Should().ThrowAsync<ArgumentNullException>();
		}

		[Fact]
		public async Task GetAsync_WithValidFunction_ReturnsResult()
		{
			int result = await Validate.GetAsync(() => Task.FromResult(42));
			result.Should().Be(42);
		}

		[Fact]
		public void Do_WithNullAction_ThrowsArgumentNullException()
		{
			var action = () => Validate.Do(null!);
			action.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Do_WithValidAction_ExecutesSuccessfully()
		{
			var executed = false;
			Validate.Do(() => executed = true);
			executed.Should().BeTrue();
		}

		[Fact]
		public void Get_WithCustomException_ThrowsCustomException()
		{
			var customException = new InvalidOperationException("Custom error");
			var action          = () => Validate.Get<object>(() => throw new(), customException);
			action.Should().Throw<InvalidOperationException>().WithMessage("Custom error");
		}

		[Fact]
		public void Get_WithCustomMessage_ThrowsExceptionWithCustomMessage()
		{
			const string customMessage = "Custom error message";
			var          action        = () => Validate.Get<object>(() => throw new(), errorMessage: customMessage);
			action.Should().Throw<Exception>().WithMessage(customMessage);
		}

		[Fact]
		public void Get_WithNullFunction_ThrowsArgumentNullException()
		{
			var action = () => Validate.Get<int>(null!);
			action.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Get_WithValidFunction_ReturnsResult()
		{
			int result = Validate.Get(() => 42);
			result.Should().Be(42);
		}
	}
}
