using System;
using System.Linq;
using System.Text;

namespace Bezoro.Core.Common.Helpers;

public static class ExceptionHelper
{
	public static void ThrowException<T>(
		object objectInstance = null,
		string methodName     = null,
		string message        = null
	)
		where T : Exception
	{
		string exceptionType = typeof(T).Name;

		string exceptionMessage = FormatExceptionMessage(
			exceptionType,
			objectInstance,
			methodName,
			message);

		throw (Exception)Activator.CreateInstance(
			typeof(T),
			exceptionMessage);
	}

	private static string FormatExceptionMessage(
		string          exceptionType,
		object          objectInstance = null,
		string          methodName     = null,
		string          message        = null,
		params object[] paramNames
	)
	{
		string objectType          = objectInstance?.GetType().Name ?? "Unknown";
		string methodPart          = !string.IsNullOrWhiteSpace(methodName) ? $".{methodName}" : string.Empty;
		string paramNamesFormatted = FormatParamNames(paramNames);

		var messageBuilder = new StringBuilder();
		messageBuilder.Append($"{exceptionType} occurred in {objectType}{methodPart}");

		if (paramNames?.Length > 0) messageBuilder.Append($" for parameters [{paramNamesFormatted}]");

		if (!string.IsNullOrWhiteSpace(message)) messageBuilder.Append($": {message}");

		return messageBuilder.ToString();
	}

	private static string FormatParamNames(params object[] paramNames)
	{
		if (paramNames == null || paramNames.Length == 0) return string.Empty;

		return string.Join(
			", ",
			paramNames.Select(p => p?.GetType().Name ?? "Unknown"));
	}
}
