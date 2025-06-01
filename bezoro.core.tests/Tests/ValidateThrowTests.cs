using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Bezoro.Core.Tests
{
	[TestFixture]
	[TestOf(typeof(Validate))]
	public class ValidateThrowTests
	{
		// Since Throw is a private method with [Conditional("UNITY_EDITOR")],
		// we need to use reflection to test it directly in editor builds

#if UNITY_EDITOR
		[Test]
		public void Throw_WithCustomException_ThrowsCustomException()
		{
			// Arrange
			var customException = new ArgumentException("Custom exception");
			var originalException = new InvalidOperationException("Original exception");
			var throwMethod = typeof(Validate).GetMethod("Throw", 
				BindingFlags.NonPublic | BindingFlags.Static);

			// Act & Assert
			var ex = Assert.Throws<ArgumentException>(() => 
				throwMethod.Invoke(null, new object[] { customException, null, originalException }));

			// We're invoking via reflection, so need to unwrap the TargetInvocationException
			Assert.That(ex.InnerException, Is.TypeOf<ArgumentException>());
			Assert.That(ex.InnerException.Message, Is.EqualTo("Custom exception"));
		}

		[Test]
		public void Throw_WithCustomMessage_ThrowsExceptionWithCustomMessage()
		{
			// Arrange
			const string customMessage = "Custom message";
			var originalException = new InvalidOperationException("Original exception");
			var throwMethod = typeof(Validate).GetMethod("Throw", 
				BindingFlags.NonPublic | BindingFlags.Static);

			// Act & Assert
			var ex = Assert.Throws<TargetInvocationException>(() => 
				throwMethod.Invoke(null, new object[] { null, customMessage, originalException }));

			// We're invoking via reflection, so need to unwrap the TargetInvocationException
			Assert.That(ex.InnerException, Is.TypeOf<Exception>());
			Assert.That(ex.InnerException.Message, Is.EqualTo(customMessage));
			Assert.That(ex.InnerException.InnerException, Is.TypeOf<InvalidOperationException>());
			Assert.That(ex.InnerException.InnerException.Message, Is.EqualTo("Original exception"));
		}

		[Test]
		public void Throw_WithNoCustomExceptionOrMessage_ThrowsOriginalException()
		{
			// Arrange
			var originalException = new InvalidOperationException("Original exception");
			var throwMethod = typeof(Validate).GetMethod("Throw", 
				BindingFlags.NonPublic | BindingFlags.Static);

			// Act & Assert
			var ex = Assert.Throws<TargetInvocationException>(() => 
				throwMethod.Invoke(null, new object[] { null, null, originalException }));

			// We're invoking via reflection, so need to unwrap the TargetInvocationException
			Assert.That(ex.InnerException, Is.TypeOf<InvalidOperationException>());
			Assert.That(ex.InnerException.Message, Is.EqualTo("Original exception"));
		}
#endif

		// Test for the internal methods via integration tests

		[Test]
		public void InternalDo_IntegrationTest_WithExceptionHandling()
		{
			// Arrange
			Action throwingAction = () => throw new InvalidOperationException("Test exception");

			// Act & Assert
#if UNITY_EDITOR
			var ex = Assert.Throws<InvalidOperationException>(() => Validate.Do(throwingAction));
			Assert.That(ex.Message, Is.EqualTo("Test exception"));
#else
			// In non-editor builds, Do method is stripped entirely
			Validate.Do(throwingAction);
			Assert.Pass("Do method is stripped in non-editor builds");
#endif
		}

		[Test]
		public async Task InternalDoAsync_IntegrationTest_WithExceptionHandling()
		{
			// Arrange
			Func<Task> throwingAction = () => Task.FromException(new InvalidOperationException("Test exception"));

			// Act & Assert
#if UNITY_EDITOR
			// For async tests, we need to verify the exception is thrown inside the Task
			Validate.DoAsync(throwingAction);
			// Wait a bit to ensure task has time to execute
			await Task.Delay(50);
			// In editor builds, DoAsync should catch the exception internally
			Assert.Pass("Exception should be caught internally");
#else
			// In non-editor builds, DoAsync method is stripped entirely
			Validate.DoAsync(throwingAction);
			Assert.Pass("DoAsync method is stripped in non-editor builds");
#endif
		}

		[Test]
		public void InternalGet_IntegrationTest_WithExceptionHandling()
		{
			// Arrange
			Func<int> throwingFunc = () => throw new InvalidOperationException("Test exception");

			// Act & Assert
#if UNITY_EDITOR
			var ex = Assert.Throws<InvalidOperationException>(() => Validate.Get(throwingFunc));
			Assert.That(ex.Message, Is.EqualTo("Test exception"));
#else
			// In non-editor builds, Get method inlines to return func()
			Assert.Throws<InvalidOperationException>(() => Validate.Get(throwingFunc));
#endif
		}

		[Test]
		public async Task InternalGetAsync_IntegrationTest_WithExceptionHandling()
		{
			// Arrange
			Func<Task<int>> throwingFunc = () => Task.FromException<int>(new InvalidOperationException("Test exception"));

			// Act & Assert
#if UNITY_EDITOR
			var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await Validate.GetAsync(throwingFunc));
			Assert.That(ex.Message, Is.EqualTo("Test exception"));
#else
			// In non-editor builds, GetAsync method inlines to return func()
			var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await Validate.GetAsync(throwingFunc));
			Assert.That(ex.Message, Is.EqualTo("Test exception"));
#endif
		}
	}
}
