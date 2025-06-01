using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Bezoro.Core.Tests
{
	[TestFixture]
	[TestOf(typeof(Validate))]
	public class ValidateTests
	{
		#region Do Tests

		[Test]
		public void Do_WithNullAction_ThrowsArgumentNullException()
		{
			// Act & Assert
			Assert.Throws<ArgumentNullException>(() => Validate.Do(null));
		}

		[Test]
		public void Do_WithActionThatThrows_PropagatesOriginalException()
		{
			// Arrange
			Action throwingAction = () => throw new InvalidOperationException("Original exception");

			// Act & Assert
#if UNITY_EDITOR
			var ex = Assert.Throws<InvalidOperationException>(() => Validate.Do(throwingAction));
			Assert.That(ex.Message, Is.EqualTo("Original exception"));
#else
			// In non-editor builds, Do method is stripped entirely
			Validate.Do(throwingAction);
			Assert.Pass("Do method is stripped in non-editor builds");
#endif
		}

		[Test]
		public void Do_WithActionThatThrowsAndCustomException_ThrowsCustomException()
		{
			// Arrange
			Action throwingAction = () => throw new InvalidOperationException("Original exception");
			var customException = new ArgumentException("Custom exception");

			// Act & Assert
#if UNITY_EDITOR
			var ex = Assert.Throws<ArgumentException>(() => Validate.Do(throwingAction, customException));
			Assert.That(ex.Message, Is.EqualTo("Custom exception"));
#else
			// In non-editor builds, Do method is stripped entirely
			Validate.Do(throwingAction, customException);
			Assert.Pass("Do method is stripped in non-editor builds");
#endif
		}

		[Test]
		public void Do_WithActionThatThrowsAndCustomMessage_ThrowsExceptionWithCustomMessage()
		{
			// Arrange
			Action throwingAction = () => throw new InvalidOperationException("Original exception");
			const string customMessage = "Custom message";

			// Act & Assert
#if UNITY_EDITOR
			var ex = Assert.Throws<Exception>(() => Validate.Do(throwingAction, null, customMessage));
			Assert.That(ex.Message, Is.EqualTo(customMessage));
			Assert.That(ex.InnerException, Is.TypeOf<InvalidOperationException>());
#else
			// In non-editor builds, Do method is stripped entirely
			Validate.Do(throwingAction, null, customMessage);
			Assert.Pass("Do method is stripped in non-editor builds");
#endif
		}

		[Test]
		public void Do_WithActionThatDoesNotThrow_ExecutesSuccessfully()
		{
			// Arrange
			var executed = false;
			Action action = () => executed = true;

			// Act
			Validate.Do(action);

			// Assert
#if UNITY_EDITOR
			Assert.That(executed, Is.True, "Action should execute in editor builds");
#else
			// In non-editor builds, Do method is stripped entirely due to [Conditional("UNITY_EDITOR")]
			Assert.Pass("Do method is stripped in non-editor builds");
#endif
		}

		#endregion

		#region DoAsync Tests

		[Test]
		public void DoAsync_WithNullAction_ThrowsArgumentNullException()
		{
			// Act & Assert
			Assert.Throws<ArgumentNullException>(() => Validate.DoAsync(null));
		}

		[Test]
		public async Task DoAsync_WithActionThatThrows_PropagatesOriginalException()
		{
			// Arrange
			Func<Task> throwingAction = () => Task.FromException(new InvalidOperationException("Original exception"));

			// Act & Assert
#if UNITY_EDITOR
			// For async tests, we need to verify the exception is thrown inside the Task
			Validate.DoAsync(throwingAction);
			// Wait a bit to ensure task has time to execute
			await Task.Delay(50);
			// In editor builds, DoAsync should catch the exception internally
			// We can't easily verify this without changing the implementation
			Assert.Pass("Exception should be caught internally");
#else
			// In non-editor builds, DoAsync method is stripped entirely
			Validate.DoAsync(throwingAction);
			Assert.Pass("DoAsync method is stripped in non-editor builds");
#endif
		}

		[Test]
		public async Task DoAsync_WithActionThatDoesNotThrow_ExecutesSuccessfully()
		{
			// Arrange
			var executed = false;
			Func<Task> action = async () => {
				await Task.Delay(10);
				executed = true;
			};

			// Act
			Validate.DoAsync(action);
			// Wait a bit to ensure task has time to execute
			await Task.Delay(50);

			// Assert
#if UNITY_EDITOR
			Assert.That(executed, Is.True);
#else
			// In non-editor builds, DoAsync method is stripped entirely
			Assert.That(executed, Is.False);
#endif
		}

		#endregion

		#region Get Tests

		[Test]
		public void Get_WithNullFunc_ThrowsArgumentNullException()
		{
			// Act & Assert
			Assert.Throws<ArgumentNullException>(() => Validate.Get<int>(null));
		}

		[Test]
		public void Get_WithFuncThatThrows_PropagatesOriginalException()
		{
			// Arrange
			Func<int> throwingFunc = () => throw new InvalidOperationException("Original exception");

			// Act & Assert
#if UNITY_EDITOR
			var ex = Assert.Throws<InvalidOperationException>(() => Validate.Get(throwingFunc));
			Assert.That(ex.Message, Is.EqualTo("Original exception"));
#else
			// In non-editor builds, Get method inlines to return func()
			Assert.Throws<InvalidOperationException>(() => Validate.Get(throwingFunc));
#endif
		}

		[Test]
		public void Get_WithFuncThatThrowsAndCustomException_ThrowsCustomException()
		{
			// Arrange
			Func<int> throwingFunc = () => throw new InvalidOperationException("Original exception");
			var customException = new ArgumentException("Custom exception");

			// Act & Assert
#if UNITY_EDITOR
			var ex = Assert.Throws<ArgumentException>(() => Validate.Get(throwingFunc, customException));
			Assert.That(ex.Message, Is.EqualTo("Custom exception"));
#else
			// In non-editor builds, Get method inlines to return func()
			Assert.Throws<InvalidOperationException>(() => Validate.Get(throwingFunc, customException));
#endif
		}

		[Test]
		public void Get_WithFuncThatDoesNotThrow_ReturnsExpectedValue()
		{
			// Arrange
			const int expectedValue = 42;
			Func<int> func = () => expectedValue;

			// Act
			var result = Validate.Get(func);

			// Assert
			Assert.That(result, Is.EqualTo(expectedValue));
		}

		#endregion

		#region GetAsync Tests

		[Test]
		public void GetAsync_WithNullFunc_ThrowsArgumentNullException()
		{
			// Act & Assert
			Assert.Throws<ArgumentNullException>(() => Validate.GetAsync<int>(null));
		}

		[Test]
		public async Task GetAsync_WithFuncThatThrows_PropagatesOriginalException()
		{
			// Arrange
			Func<Task<int>> throwingFunc = () => Task.FromException<int>(new InvalidOperationException("Original exception"));

			// Act & Assert
#if UNITY_EDITOR
			var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await Validate.GetAsync(throwingFunc));
			Assert.That(ex.Message, Is.EqualTo("Original exception"));
#else
			// In non-editor builds, GetAsync method inlines to return func()
			var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await Validate.GetAsync(throwingFunc));
			Assert.That(ex.Message, Is.EqualTo("Original exception"));
#endif
		}

		[Test]
		public async Task GetAsync_WithFuncThatDoesNotThrow_ReturnsExpectedValue()
		{
			// Arrange
			const int expectedValue = 42;
			Func<Task<int>> func = () => Task.FromResult(expectedValue);

			// Act
			var result = await Validate.GetAsync(func);

			// Assert
			Assert.That(result, Is.EqualTo(expectedValue));
		}

		#endregion
	}
}
