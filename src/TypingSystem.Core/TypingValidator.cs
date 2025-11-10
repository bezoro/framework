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
		public static TypingResult ValidateInput(ReadOnlySpan<char> target, byte position, char inputChar)
		{
			byte length = (byte)target.Length;

			if (length == 0)
			{
				return TypingResult.EmptyTarget(position, inputChar);
			}

			if (position >= length)
			{
				return TypingResult.PositionOutOfRange(position, length, inputChar);
			}

			char expectedChar = target[position];
			if (inputChar != expectedChar)
			{
				return TypingResult.Mismatch(expectedChar, position, inputChar, length);
			}

			bool completes = position + 1 == length;
			return completes
				? TypingResult.Completed(expectedChar, position, inputChar, length)
				: TypingResult.Match(expectedChar, position, inputChar, length);
		}
	}
}
