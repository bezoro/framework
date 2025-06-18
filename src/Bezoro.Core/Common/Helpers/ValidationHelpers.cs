using System;
using System.Collections.Generic;
using System.Text;

namespace Bezoro.Core.Common.Helpers
{
	public class ValidationHelpers
	{
		public static void Condition(
			bool condition,
			string errorMessage,
			object caller = null,
			string methodName = null
		)
		{
			if (!condition)
			{
				ExceptionHelpers.ThrowException<InvalidOperationException>(
					caller,
					methodName ?? string.Empty,
					errorMessage
				);
			}
		}

		public static void IsFalse(bool condition, string errorMessage = "") =>
			IsFalse<InvalidOperationException>(
				condition,
				errorMessage);

		public static void IsFalse<TException>(bool condition, string errorMessage = "")
			where TException : Exception
		{
			if (condition)
			{
				ExceptionHelpers.ThrowException<TException>(errorMessage);
			}
		}

		public static void IsNotNull<T>(T obj) where T : class
		{
			if (obj == null)
			{
				throw new ArgumentNullException(nameof(obj));
			}
		}

		public static void IsPositiveValue(
			float value,
			string paramName = "value",
			object caller = null,
			string methodName = null
		)
		{
			if (value <= 0)
			{
				ExceptionHelpers.ThrowException<ArgumentException>(
					caller,
					methodName ?? string.Empty,
					$"{paramName} must be positive. Received: {value}"
				);
			}
		}

		public static void IsSubclassOf<T>(object caller, string methodName, Type type)
		{
			if (!type.IsSubclassOf(typeof(T)))
			{
				ExceptionHelpers.ThrowException<ArgumentException>(
					caller,
					methodName,
					$"Type {type} is not a subclass of {typeof(T).Name}"
				);
			}
		}

		/// <summary>
		///     Validates that both file and rank coordinates are within the specified range
		/// </summary>
		/// <param name="file">The file coordinate</param>
		/// <param name="rank">The rank coordinate</param>
		/// <param name="min">Minimum allowed value (inclusive)</param>
		/// <param name="max">Maximum allowed value (inclusive)</param>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when coordinates are outside the specified range</exception>
		public static void IsWithinRange(int file, int rank, int min, int max)
		{
			if (file < min || file > max || rank < min || rank > max)
			{
				throw new ArgumentOutOfRangeException(
					$"Coordinates must be between {min} and {max}. Received: file={file}, rank={rank}");
			}
		}

		public static void ListNotNullOrEmpty<T>(
			List<T> list,
			string paramName = "list",
			object caller = null,
			string methodName = null
		)
		{
			if (list == null || list.Count == 0)
			{
				ExceptionHelpers.ThrowException<ArgumentException>(
					caller,
					methodName ?? string.Empty,
					$"{paramName} is null or empty"
				);
			}
		}

		public static void ObjectNotNull(
			object objectToValidate,
			string paramName = null,
			string exceptionMessage = null,
			object caller = null,
			string methodName = null
		)
		{
			if (objectToValidate != null)
			{
				return;
			}

			var messageBuilder = new StringBuilder();
			if (string.IsNullOrEmpty(paramName))
			{
				messageBuilder.Append("Object is null");
			}
			else
			{
				messageBuilder.Append($"{paramName} is null");
			}

			if (!string.IsNullOrWhiteSpace(exceptionMessage))
			{
				messageBuilder.Append($"; {exceptionMessage}");
			}

			ExceptionHelpers.ThrowException<ArgumentNullException>(
				caller,
				methodName ?? string.Empty,
				messageBuilder.ToString()
			);
		}

		public static void String(
			string value,
			string paramName = "value",
			object caller = null,
			string methodName = null
		)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				ExceptionHelpers.ThrowException<ArgumentException>(
					caller,
					methodName ?? string.Empty,
					$"{paramName} is null or empty"
				);
			}
		}

		public static void ValueNotAboveMax(
			int value,
			int max,
			string valueName = "value",
			string maxName = "max",
			object caller = null,
			string methodName = null
		)
		{
			if (value > max)
			{
				ExceptionHelpers.ThrowException<ArgumentException>(
					caller,
					methodName ?? string.Empty,
					$"{valueName} cannot be greater than {maxName}. Received: {value}, Max: {max}"
				);
			}
		}
	}
}
