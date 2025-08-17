using System;
using System.Runtime.CompilerServices;

namespace Bezoro.Core.Common.Extensions;

public static class CharExtensions
{
	/// <summary>
	///     Determines whether the specified character is empty (whitespace, tab, newline, etc.).
	/// </summary>
	/// <param name="c">The character to check.</param>
	/// <returns>true if the character is empty; otherwise, false.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsEmpty(this char c)
	{
		c.ThrowIfNull();
		return char.IsWhiteSpace(c);
	}

	/// <summary>
	///     Throws if the specified character is empty (whitespace, tab, newline, etc.).
	/// </summary>
	/// <param name="c">The character to check.</param>
	/// <returns>The input character if it's not empty.</returns>
	/// <exception cref="ArgumentException">Thrown when the character is empty.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static char ThrowIfEmpty(this char c)
	{
		c.ThrowIfNull();
		return c.IsEmpty() ? throw new ArgumentException("Empty char not allowed", nameof(c)) : c;
	}

	/// <summary>
	///     Throws if the specified character is a letter.
	/// </summary>
	/// <param name="c">The character to check.</param>
	/// <returns>The input character if it's not a letter.</returns>
	/// <exception cref="ArgumentException">Thrown when the character is a letter.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static char ThrowIfLetter(this char c)
	{
		c.ThrowIfNull().ThrowIfEmpty();
		return char.IsLetter(c) ? throw new ArgumentException("Letter char not allowed", nameof(c)) : c;
	}

	/// <summary>
	///     Throws if the specified character is lowercase.
	/// </summary>
	/// <param name="c">The character to check.</param>
	/// <returns>The input character if it's not lowercase.</returns>
	/// <exception cref="ArgumentException">Thrown when the character is lowercase.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static char ThrowIfLowerCase(this char c)
	{
		c.ThrowIfNull().ThrowIfEmpty();
		return char.IsLower(c) ? throw new ArgumentException("Lowercase char not allowed", nameof(c)) : c;
	}

	/// <summary>
	///     Throws if the specified character is a number.
	/// </summary>
	/// <param name="c">The character to check.</param>
	/// <returns>The input character if it's not a number.</returns>
	/// <exception cref="ArgumentException">Thrown when the character is a number.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static char ThrowIfNumber(this char c)
	{
		c.ThrowIfNull().ThrowIfEmpty();
		return char.IsNumber(c) ? throw new ArgumentException("Number char not allowed", nameof(c)) : c;
	}

	/// <summary>
	///     Throws if the specified character is a symbol.
	/// </summary>
	/// <param name="c">The character to check.</param>
	/// <returns>The input character if it's not a symbol.</returns>
	/// <exception cref="ArgumentException">Thrown when the character is a symbol.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static char ThrowIfSymbol(this char c)
	{
		c.ThrowIfNull().ThrowIfEmpty();
		return char.IsSymbol(c) ? throw new ArgumentException("Symbol char not allowed", nameof(c)) : c;
	}

	/// <summary>
	///     Throws if the specified character is uppercase.
	/// </summary>
	/// <param name="c">The character to check.</param>
	/// <returns>The input character if it's not uppercase.</returns>
	/// <exception cref="ArgumentException">Thrown when the character is uppercase.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static char ThrowIfUpperCase(this char c)
	{
		c.ThrowIfNull().ThrowIfEmpty();
		return char.IsUpper(c) ? throw new ArgumentException("Uppercase char not allowed", nameof(c)) : c;
	}
}
