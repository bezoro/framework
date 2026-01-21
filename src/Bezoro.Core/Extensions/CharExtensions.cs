using System.Runtime.CompilerServices;

namespace Bezoro.Core.Extensions;

/// <summary>
///     Provides extension methods for <see cref="char" /> to perform common validation and checking operations.
/// </summary>
public static class CharExtensions
{
	/// <summary>
	///     Determines whether the specified character is considered "empty", i.e., a whitespace character (such as space, tab,
	///     newline, etc.).
	/// </summary>
	/// <param name="c">The character to test for emptiness.</param>
	/// <returns><c>true</c> if <paramref name="c" /> is a whitespace character; otherwise, <c>false</c>.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsEmpty(this char c)
	{
		c.ThrowIfNull();
		return char.IsWhiteSpace(c);
	}

	/// <summary>
	///     Throws an <see cref="ArgumentException" /> if the specified character is empty (whitespace, tab, newline, etc.).
	/// </summary>
	/// <param name="c">The character to validate.</param>
	/// <returns>The input character if it is not empty.</returns>
	/// <exception cref="ArgumentException">Thrown when the character is empty.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static char ThrowIfEmpty(this char c)
	{
		c.ThrowIfNull();
		return c.IsEmpty() ? throw new ArgumentException("Empty char not allowed", nameof(c)) : c;
	}

	/// <summary>
	///     Throws an <see cref="ArgumentException" /> if the specified character is a letter.
	/// </summary>
	/// <param name="c">The character to validate.</param>
	/// <returns>The input character if it is not a letter.</returns>
	/// <exception cref="ArgumentException">Thrown when the character is a letter.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static char ThrowIfLetter(this char c)
	{
		c.ThrowIfNull().ThrowIfEmpty();
		return char.IsLetter(c) ? throw new ArgumentException("Letter char not allowed", nameof(c)) : c;
	}

	/// <summary>
	///     Throws an <see cref="ArgumentException" /> if the specified character is a lowercase letter.
	/// </summary>
	/// <param name="c">The character to validate.</param>
	/// <returns>The input character if it is not lowercase.</returns>
	/// <exception cref="ArgumentException">Thrown when the character is lowercase.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static char ThrowIfLowerCase(this char c)
	{
		c.ThrowIfNull().ThrowIfEmpty();
		return char.IsLower(c) ? throw new ArgumentException("Lowercase char not allowed", nameof(c)) : c;
	}

	/// <summary>
	///     Throws an <see cref="ArgumentException" /> if the specified character is a number.
	/// </summary>
	/// <param name="c">The character to validate.</param>
	/// <returns>The input character if it is not a number.</returns>
	/// <exception cref="ArgumentException">Thrown when the character is a number.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static char ThrowIfNumber(this char c)
	{
		c.ThrowIfNull().ThrowIfEmpty();
		return char.IsNumber(c) ? throw new ArgumentException("Number char not allowed", nameof(c)) : c;
	}

	/// <summary>
	///     Throws an <see cref="ArgumentException" /> if the specified character is a symbol.
	/// </summary>
	/// <param name="c">The character to validate.</param>
	/// <returns>The input character if it is not a symbol.</returns>
	/// <exception cref="ArgumentException">Thrown when the character is a symbol.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static char ThrowIfSymbol(this char c)
	{
		c.ThrowIfNull().ThrowIfEmpty();
		return char.IsSymbol(c) ? throw new ArgumentException("Symbol char not allowed", nameof(c)) : c;
	}

	/// <summary>
	///     Throws an <see cref="ArgumentException" /> if the specified character is an uppercase letter.
	/// </summary>
	/// <param name="c">The character to validate.</param>
	/// <returns>The input character if it is not uppercase.</returns>
	/// <exception cref="ArgumentException">Thrown when the character is uppercase.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static char ThrowIfUpperCase(this char c)
	{
		c.ThrowIfNull().ThrowIfEmpty();
		return char.IsUpper(c) ? throw new ArgumentException("Uppercase char not allowed", nameof(c)) : c;
	}
}
