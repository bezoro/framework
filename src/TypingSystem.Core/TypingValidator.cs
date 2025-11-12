using System;
using System.Runtime.CompilerServices;

namespace TypingSystem.Core
{
	public static class TypingValidator
	{
		/// <summary>
		/// Validates the input character against the target sequence at the given position.
		/// Expected to be used to validate words one by one.
		/// </summary>
		/// <param name="target">The target sequence to validate against.</param>
		/// <param name="position">The position in the target sequence to validate against.</param>
		/// <param name="inputChar">The input character to validate.</param>
		/// <returns>The result of the validation.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static TypingResult ValidateInput(ReadOnlySpan<char> target, byte position, char inputChar, TypingValidatorOptions? options = null)
		{
			int targetLength = target.Length;
			if (targetLength > byte.MaxValue)
			{
				throw new ArgumentOutOfRangeException(
					nameof(target),
					targetLength,
					"Target length cannot exceed 255 characters.");
			}

			byte length = (byte)targetLength;

			if (length == 0)
			{
				return Dispatch(options, TypingResult.EmptyTarget(position, inputChar));
			}

			if (position >= length)
			{
				return Dispatch(options, TypingResult.PositionOutOfRange(position, length, inputChar));
			}

			char expectedChar = target[position];

			bool ignoreCase = options?.IgnoreCase ?? false;
			bool isMatch = ignoreCase
				? char.ToUpperInvariant(inputChar) == char.ToUpperInvariant(expectedChar)
				: inputChar == expectedChar;

			if (!isMatch)
			{
				return Dispatch(options, TypingResult.Mismatch(expectedChar, position, inputChar, length));
			}

			bool completes = position + 1 == length;
			return Dispatch(
				options,
				completes
					? TypingResult.Completed(expectedChar, position, inputChar, length)
					: TypingResult.Match(expectedChar, position, inputChar, length));

			static TypingResult Dispatch(TypingValidatorOptions? opts, TypingResult result)
			{
				opts?.Notify(result);
				return result;
			}
		}
	}
}
