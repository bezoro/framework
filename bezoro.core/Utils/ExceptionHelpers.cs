using System;

namespace Bezoro.Core.Utils
{
	public static class ExceptionHelpers
	{
		public static void ThrowException<TException>(
			object caller,
			string methodName,
			string errorMessage
		) where TException : Exception
		{
			string fullMessage = $"{(caller != null ? caller.GetType().Name : "")}::{methodName}: {errorMessage}";
			throw (TException)Activator.CreateInstance(typeof(TException), fullMessage);
		}

		public static void ThrowException<TException>(string errorMessage) where TException : Exception
		{
			throw (TException)Activator.CreateInstance(typeof(TException), errorMessage);
		}
	}
}
