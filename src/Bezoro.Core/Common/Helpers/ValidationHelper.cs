using System.Text;
using Bezoro.Core.Common.Extensions;

namespace Bezoro.Core.Common.Helpers;

/// <summary>
///     Provides helper static methods for parameter, invariant, and value validation,
///     throwing standard exceptions when preconditions are violated.
/// </summary>
public static class ValidationHelper
{
	/// <summary>
	///     Throws an exception if the specified <paramref name="condition" /> is false.
	///     Uses <see cref="InvalidOperationException" />.
	/// </summary>
	/// <param name="condition">The condition that should hold.</param>
	/// <param name="errorMessage">The message to include in the exception.</param>
	/// <param name="caller">Optional object context for exception trace.</param>
	/// <param name="methodName">Optional calling method name.</param>
	/// <exception cref="InvalidOperationException">If <paramref name="condition" /> is false.</exception>
	public static void Condition(
		bool    condition,
		string  errorMessage,
		object? caller     = null,
		string? methodName = null
	)
	{
		if (!condition)
			ExceptionHelper.ThrowException<InvalidOperationException>(
				caller,
				methodName ?? string.Empty,
				errorMessage
			);
	}

	/// <summary>
	///     Validates that the specified condition is <c>false</c>. Throws <see cref="InvalidOperationException" /> if not.
	/// </summary>
	/// <param name="condition">The condition that must not be true.</param>
	/// <param name="errorMessage">The message for the exception.</param>
	/// <exception cref="InvalidOperationException">If <paramref name="condition" /> is true.</exception>
	public static void IsFalse(bool condition, string errorMessage = "") =>
		IsFalse<InvalidOperationException>(
			condition,
			errorMessage);

	/// <summary>
	///     Validates that the specified condition is <c>false</c>.
	///     Throws an exception of type <typeparamref name="TException" /> if <paramref name="condition" /> is <c>true</c>.
	/// </summary>
	/// <typeparam name="TException">
	///     The type of exception to throw. Must have a public constructor accepting a
	///     <see cref="string" /> message.
	/// </typeparam>
	/// <param name="condition">The condition that must be <c>false</c>.</param>
	/// <param name="errorMessage">The error message to include in the exception.</param>
	/// <remarks>
	///     Throws an exception of type <typeparamref name="TException" /> if <paramref name="condition" /> is <c>true</c>.
	/// </remarks>
	/// <exception cref="System.Exception">
	///     An exception of type <typeparamref name="TException" /> is thrown if <paramref name="condition" /> is <c>true</c>.
	/// </exception>
	public static void IsFalse<TException>(bool condition, string errorMessage = "")
		where TException : Exception
	{
		if (condition) ExceptionHelper.ThrowException<TException>(errorMessage);
	}

	/// <summary>
	///     Throws <see cref="ArgumentNullException" /> if the argument is <c>null</c>.
	/// </summary>
	/// <typeparam name="T">The reference type of the argument.</typeparam>
	/// <param name="obj">The object to check for <c>null</c>.</param>
	/// <exception cref="ArgumentNullException">If <paramref name="obj" /> is <c>null</c>.</exception>
	public static void IsNotNull<T>(T obj) where T : class
	{
		if (obj == null) throw new ArgumentNullException(nameof(obj));
	}

	/// <summary>
	///     Throws <see cref="ArgumentException" /> if <paramref name="value" /> is not a positive number (&gt;0).
	/// </summary>
	/// <param name="value">The float value to validate.</param>
	/// <param name="paramName">The name of the parameter checked.</param>
	/// <param name="caller">Optional object context for exception trace.</param>
	/// <param name="methodName">Optional calling method name.</param>
	/// <exception cref="ArgumentException">If <paramref name="value" /> is not positive.</exception>
	public static void IsPositiveValue(
		float   value,
		string  paramName  = "value",
		object? caller     = null,
		string? methodName = null
	)
	{
		if (value <= 0)
			ExceptionHelper.ThrowException<ArgumentException>(
				caller,
				methodName ?? string.Empty,
				$"{paramName} must be positive. Received: {value}"
			);
	}

	/// <summary>
	///     Throws <see cref="ArgumentException" /> if <paramref name="type" /> is not a subclass of <typeparamref name="T" />.
	/// </summary>
	/// <typeparam name="T">The base type to check against.</typeparam>
	/// <param name="caller">Optional object instance (for exception context).</param>
	/// <param name="methodName">Optional name of the calling method.</param>
	/// <param name="type">The <see cref="Type" /> to validate.</param>
	/// <exception cref="ArgumentException">If <paramref name="type" /> is not a subclass of <typeparamref name="T" />.</exception>
	public static void IsSubclassOf<T>(object caller, string methodName, Type type)
	{
		if (!type.IsSubclassOf(typeof(T)))
			ExceptionHelper.ThrowException<ArgumentException>(
				caller,
				methodName,
				$"Type {type} is not a subclass of {typeof(T).Name}"
			);
	}

	/// <summary>
	///     Validates that both <paramref name="file" /> and <paramref name="rank" /> are within [<paramref name="min" />,
	///     <paramref name="max" />] (inclusive).
	/// </summary>
	/// <param name="file">The file coordinate.</param>
	/// <param name="rank">The rank coordinate.</param>
	/// <param name="min">Minimum allowed value (inclusive).</param>
	/// <param name="max">Maximum allowed value (inclusive).</param>
	/// <exception cref="ArgumentOutOfRangeException">
	///     Thrown if <paramref name="file" /> or <paramref name="rank" /> are outside the specified range.
	/// </exception>
	public static void IsWithinRange(int file, int rank, int min, int max)
	{
		if (file < min || file > max || rank < min || rank > max)
			throw new ArgumentOutOfRangeException(
				$"Coordinates must be between {min} and {max}. Received: file={file}, rank={rank}");
	}

	/// <summary>
	///     Validates that a <see cref="List{T}" /> is not <c>null</c> and not empty.
	///     Throws <see cref="ArgumentException" /> if null or empty.
	/// </summary>
	/// <typeparam name="T">The list element type.</typeparam>
	/// <param name="list">The list to check.</param>
	/// <param name="paramName">The name of the parameter representing the list.</param>
	/// <param name="caller">Optional object context for exception trace.</param>
	/// <param name="methodName">Optional calling method name.</param>
	/// <exception cref="ArgumentException">If the list is null or empty.</exception>
	public static void ListNotNullOrEmpty<T>(
		List<T> list,
		string  paramName  = "list",
		object? caller     = null,
		string? methodName = null
	)
	{
		list.ThrowIfNull();

		if (list.Count == 0)
			ExceptionHelper.ThrowException<ArgumentException>(
				caller,
				methodName ?? string.Empty,
				$"{paramName} is null or empty"
			);
	}

	/// <summary>
	///     Validates that a string is not <c>null</c> or whitespace.
	///     Throws <see cref="ArgumentException" /> if validation fails.
	/// </summary>
	/// <param name="value">The string parameter to check.</param>
	/// <param name="paramName">Name of the parameter.</param>
	/// <param name="caller">Optional object context for exception trace.</param>
	/// <param name="methodName">Optional calling method name.</param>
	/// <exception cref="ArgumentException">If string is null or whitespace.</exception>
	public static void String(
		string  value,
		string  paramName  = "value",
		object? caller     = null,
		string? methodName = null
	)
	{
		if (string.IsNullOrWhiteSpace(value))
			ExceptionHelper.ThrowException<ArgumentException>(
				caller,
				methodName ?? string.Empty,
				$"{paramName} is null or empty"
			);
	}

	/// <summary>
	///     Throws <see cref="ArgumentNullException" /> if <paramref name="objectToValidate" /> is <c>null</c>.
	/// </summary>
	/// <param name="objectToValidate">The object to check.</param>
	/// <param name="paramName">The parameter/property name, if applicable.</param>
	/// <param name="exceptionMessage">Extra error message to append, if any.</param>
	/// <param name="caller">Optional object context for exception trace.</param>
	/// <param name="methodName">Optional calling method name.</param>
	/// <exception cref="ArgumentNullException">If object is null.</exception>
	public static void ThrowIfObjectIsNull(
		object  objectToValidate,
		string? paramName        = null,
		string? exceptionMessage = null,
		object? caller           = null,
		string? methodName       = null
	)
	{
		objectToValidate.ThrowIfNull();

		var messageBuilder = new StringBuilder();
		messageBuilder.Append(string.IsNullOrEmpty(paramName) ? "Object is null" : $"{paramName} is null");

		if (!string.IsNullOrWhiteSpace(exceptionMessage)) messageBuilder.Append($"; {exceptionMessage}");

		ExceptionHelper.ThrowException<ArgumentNullException>(
			caller,
			methodName ?? string.Empty,
			messageBuilder.ToString()
		);
	}

	/// <summary>
	///     Validates that <paramref name="value" /> is not greater than <paramref name="max" />.
	///     Throws <see cref="ArgumentException" /> if validation fails.
	/// </summary>
	/// <param name="value">The value to check.</param>
	/// <param name="max">Maximum allowed value (inclusive).</param>
	/// <param name="valueName">Parameter/property name for value.</param>
	/// <param name="maxName">Parameter/property name for maximum permitted value.</param>
	/// <param name="caller">Optional object context for exception trace.</param>
	/// <param name="methodName">Optional calling method name.</param>
	/// <exception cref="ArgumentException">If value exceeds maximum.</exception>
	public static void ValueNotAboveMax(
		int     value,
		int     max,
		string  valueName  = "value",
		string  maxName    = "max",
		object? caller     = null,
		string? methodName = null
	)
	{
		if (value > max)
			ExceptionHelper.ThrowException<ArgumentException>(
				caller,
				methodName ?? string.Empty,
				$"{valueName} cannot be greater than {maxName}. Received: {value}, Max: {max}"
			);
	}
}
