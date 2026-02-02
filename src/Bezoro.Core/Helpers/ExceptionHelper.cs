using System.Text;
using Bezoro.Core.Extensions;

namespace Bezoro.Core.Helpers;

/// <summary>
///     Provides helper methods for constructing and throwing exceptions with rich context information.
/// </summary>
public static class ExceptionHelper
{
	/// <summary>
	///     Throws an exception of the specified type <typeparamref name="T" /> with a formatted message
	///     including optional object instance, method name, and additional message.
	/// </summary>
	/// <typeparam name="T">The exception type to be thrown.</typeparam>
	/// <param name="objectInstance">The instance on which the exception occurred, or <c>null</c> if unknown.</param>
	/// <param name="methodName">The name of the method where the exception occurred (optional).</param>
	/// <param name="message">The detailed error message (optional).</param>
	/// <exception cref="InvalidOperationException">
	///     Thrown if exception of type <typeparamref name="T" /> could not be created via reflection.
	/// </exception>
	/// <remarks>
	///     The generated exception message will include the exception type, (optionally) the object type, method name, and
	///     error message.
	/// </remarks>
	public static void ThrowException<T>(
		object? objectInstance = null,
		string? methodName     = null,
		string? message        = null
	)
		where T : Exception
	{
		string exceptionType = typeof(T).Name;

		string exceptionMessage = FormatExceptionMessage(
			exceptionType,
			objectInstance,
			methodName,
			message
		);

		var exception = Activator.CreateInstance(typeof(T), exceptionMessage) as Exception;
		if (exception is null)
			throw new InvalidOperationException($"Could not create exception of type {typeof(T).FullName}.");

		throw exception;
	}

	/// <summary>
	///     Throws an exception of the specified type <typeparamref name="TException" /> with the provided error message.
	/// </summary>
	/// <typeparam name="TException">The exception type to throw.</typeparam>
	/// <param name="errorMessage">The error message to include in the exception.</param>
	/// <exception cref="InvalidOperationException">
	///     Thrown if exception of type <typeparamref name="TException" /> could not be created via reflection.
	/// </exception>
	public static void ThrowException<TException>(string errorMessage) where TException : Exception
	{
		var exception = Activator.CreateInstance(typeof(TException), errorMessage) as TException;
		if (exception is null)
			throw new InvalidOperationException($"Could not create exception of type {typeof(TException).FullName}.");

		throw exception;
	}

	/// <summary>
	///     Formats a detailed exception message including exception type, object context, method, parameter names and an
	///     additional message.
	/// </summary>
	/// <param name="exceptionType">The type name of the exception.</param>
	/// <param name="objectInstance">The instance on which the exception occurred (optional).</param>
	/// <param name="methodName">The name of the method where the exception occurred (optional).</param>
	/// <param name="message">The detailed message explaining the error (optional).</param>
	/// <param name="paramNames">
	///     A list of parameter names or objects to identify which parameters are related to the exception (optional).
	/// </param>
	/// <returns>A formatted string suitable for use as an exception message.</returns>
	private static string FormatExceptionMessage(
		string          exceptionType,
		object?         objectInstance = null,
		string?         methodName     = null,
		string?         message        = null,
		params object[] paramNames
	)
	{
		string objectType          = objectInstance?.GetType().Name ?? "Unknown";
		string methodPart          = !string.IsNullOrWhiteSpace(methodName) ? $".{methodName}" : string.Empty;
		string paramNamesFormatted = FormatParamNames(paramNames);

		var messageBuilder = new StringBuilder();
		messageBuilder.Append($"{exceptionType} occurred in {objectType}{methodPart}");

		if (paramNames.Length > 0) messageBuilder.Append($" for parameters [{paramNamesFormatted}]");

		if (!string.IsNullOrWhiteSpace(message)) messageBuilder.Append($": {message}");

		return messageBuilder.ToString();
	}

	/// <summary>
	///     Formats parameter information for inclusion in an exception message.
	/// </summary>
	/// <param name="paramNames">A list of parameter objects to be formatted.</param>
	/// <returns>
	///     A comma-separated string of parameter type names, or an empty string if <paramref name="paramNames" /> is empty.
	/// </returns>
	private static string FormatParamNames(params object[] paramNames)
	{
		paramNames.ThrowIfNull();
		return paramNames.Length == 0
				   ? string.Empty
				   : string.Join(", ", paramNames.Select(p => p?.GetType().Name ?? "Unknown"));
	}
}
