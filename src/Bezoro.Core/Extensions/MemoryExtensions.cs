using System.Runtime.CompilerServices;

namespace Bezoro.Core.Extensions;

/// <summary>
///     Provides extension methods for <see cref="Memory{T}" /> and <see cref="ReadOnlyMemory{T}" /> to enforce
///     non-emptiness.
/// </summary>
public static class MemoryExtensions
{
	/// <summary>
	///     Throws an <see cref="ArgumentException" /> if the <paramref name="memory" /> sequence is empty.
	/// </summary>
	/// <typeparam name="T">The type of elements in the memory sequence.</typeparam>
	/// <param name="memory">The <see cref="Memory{T}" /> instance to validate.</param>
	/// <param name="paramName">The expression or parameter name for the exception message (automatically supplied).</param>
	/// <returns>The same <see cref="Memory{T}" /> instance if it is not empty.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="memory" /> is empty.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Memory<T> ThrowIfEmpty<T>(
		this                                 Memory<T> memory,
		[CallerArgumentExpression("memory")] string?   paramName = null)
	{
		if (memory.Length == 0)
			throw new ArgumentException("Sequence cannot be empty.", paramName);

		return memory;
	}

	/// <summary>
	///     Throws an <see cref="ArgumentException" /> if the <paramref name="memory" /> sequence is empty.
	/// </summary>
	/// <typeparam name="T">The type of elements in the read-only memory sequence.</typeparam>
	/// <param name="memory">The <see cref="ReadOnlyMemory{T}" /> instance to validate.</param>
	/// <param name="paramName">The expression or parameter name for the exception message (automatically supplied).</param>
	/// <returns>The same <see cref="ReadOnlyMemory{T}" /> instance if it is not empty.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="memory" /> is empty.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ReadOnlyMemory<T> ThrowIfEmpty<T>(
		this                                 ReadOnlyMemory<T> memory,
		[CallerArgumentExpression("memory")] string?           paramName = null)
	{
		if (memory.Length == 0)
			throw new ArgumentException("Sequence cannot be empty.", paramName);

		return memory;
	}
}
