using System;

namespace Bezoro.Core.Common.Helpers;

public static class ExceptionHelpers
{
	public static void ThrowException<TException>(
		object caller,
		string methodName,
		string errorMessage
	) where TException : Exception
	{
		var fullMessage = $"{(caller != null ? caller.GetType().Name : "")}::{methodName}: {errorMessage}";
		throw (TException)Activator.CreateInstance(typeof(TException), fullMessage);
	}

	public static void ThrowException<TException>(string errorMessage) where TException : Exception =>
		throw ((TException)Activator.CreateInstance(typeof(TException), errorMessage));
}
