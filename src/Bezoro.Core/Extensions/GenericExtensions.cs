using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Bezoro.Core.Types;
using Bezoro.Core.Types.Exceptions;

namespace Bezoro.Core.Extensions;

/// <summary>
///     A collection of frequently-used helper methods that extend the capabilities of any type.
///     All members are implemented as pure functions (no side-effects) and therefore can be safely used in
///     LINQ queries, unit-tests or anywhere short, expressive code is preferred.
/// </summary>
public static class GenericExtensions
{
	private static readonly ConcurrentDictionary<ExpressionCacheKey, Delegate> ExpressionCache = new();

	/// <summary>
	///     Determines whether <paramref name="value" /> is within the inclusive range defined by <paramref name="min" /> and
	///     <paramref name="max" />.
	/// </summary>
	/// <typeparam name="T">A type that implements <see cref="IComparable{T}" />.</typeparam>
	/// <param name="value">The value to check.</param>
	/// <param name="min">The minimum (inclusive) value.</param>
	/// <param name="max">The maximum (inclusive) value.</param>
	/// <returns>
	///     <c>true</c> if <paramref name="value" /> is greater than or equal to <paramref name="min" /> and less than or
	///     equal to <paramref name="max" />; otherwise, <c>false</c>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///     Thrown if any argument is <c>null</c>.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsBetween<T>(this T value, T min, T max) where T : IComparable<T>
	{
		value.ThrowIfNull();
		min.ThrowIfNull();
		max.ThrowIfNull();

		return value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0;
	}

	/// <summary>
	///     Determines whether <paramref name="value" /> equals the default value for its type.
	/// </summary>
	/// <typeparam name="T">A value type.</typeparam>
	/// <param name="value">The value to check.</param>
	/// <returns><c>true</c> if <paramref name="value" /> equals <c>default(T)</c>; otherwise, <c>false</c>.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsDefault<T>(this T value) where T : struct =>
		EqualityComparer<T>.Default.Equals(value, default);

	/// <summary>
	///     Determines whether a <see langword="nullable" /> value is <see langword="null" />.
	/// </summary>
	/// <typeparam name="T">The type of the input.</typeparam>
	/// <param name="value">The value to check for null.</param>
	/// <returns><c>true</c> if <paramref name="value" /> is <c>null</c>; otherwise, <c>false</c>.</returns>
	/// <remarks>
	///     An unconstrained generic <typeparamref name="T" /> is used so that both reference and value-types wrapped in
	///     <c>Nullable&lt;T&gt;</c> are supported.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsNull<T>([NotNullWhen(false)] this T? value) => value is null;

	/// <summary>
	///     Determines whether <paramref name="value" /> is equal to any element in <paramref name="candidates" />.
	/// </summary>
	/// <typeparam name="T">The type being compared.</typeparam>
	/// <param name="value">The source value to test.</param>
	/// <param name="candidates">One or more values to compare against.</param>
	/// <returns>
	///     <c>true</c> if <paramref name="value" /> equals any element in <paramref name="candidates" />; otherwise,
	///     <c>false</c>.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///     Thrown if <paramref name="value" /> or <paramref name="candidates" /> is
	///     <c>null</c>.
	/// </exception>
	/// <exception cref="ArgumentException">Thrown if <paramref name="candidates" /> is empty.</exception>
	/// <example>
	///     <code>
	/// if (statusCode.IsOneOf(200, 201, 202)) { ... }
	/// </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsOneOf<T>(this T value, params T[] candidates)
	{
		value.ThrowIfNull();
		candidates.ThrowIfNull();
		candidates.ThrowIfEmpty();

		return candidates.Contains(value);
	}

#if NET9_0_OR_GREATER
	/// <summary>
	///     Determines whether <paramref name="value" /> is equal to any element in <paramref name="candidates" />.
	///     This overload avoids array allocation by using <see cref="ReadOnlySpan{T}" />.
	/// </summary>
	/// <typeparam name="T">The type being compared, must implement <see cref="IEquatable{T}" />.</typeparam>
	/// <param name="value">The source value to test.</param>
	/// <param name="candidates">One or more values to compare against.</param>
	/// <returns>
	///     <c>true</c> if <paramref name="value" /> equals any element in <paramref name="candidates" />; otherwise,
	///     <c>false</c>.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="candidates" /> is empty.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsOneOf<T>(this T value, params ReadOnlySpan<T> candidates) where T : IEquatable<T>?
	{
		if (candidates.IsEmpty)
			ThrowEmptyCandidates();

		foreach (var candidate in candidates)
		{
			if (EqualityComparer<T>.Default.Equals(value, candidate))
				return true;
		}

		return false;
	}

	[DoesNotReturn]
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void ThrowEmptyCandidates() =>
		throw new ArgumentException("Candidates cannot be empty.", "candidates");
#endif

	/// <summary>
	///     Wraps a single item into an <see cref="IEnumerable{T}" /> containing that item.
	///     Handy for providing single elements to APIs expecting an enumerable.
	/// </summary>
	/// <typeparam name="T">The type of the item.</typeparam>
	/// <param name="item">The item to yield.</param>
	/// <returns><see cref="IEnumerable{T}" /> containing the specified <paramref name="item" />.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="item" /> is <c>null</c>.</exception>
	/// <remarks>
	///     Returns a zero-allocation struct enumerable instead of using iterator state machine.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static SingleItemEnumerable<T> Yield<T>(this T item)
	{
		item.ThrowIfNull();

		return new(item);
	}

	/// <summary>
	///     Throws an exception if the provided predicate expression evaluates to <c>true</c> for the specified value.
	/// </summary>
	/// <typeparam name="T">The type of the value being checked.</typeparam>
	/// <param name="value">The value to check.</param>
	/// <param name="predicate">The predicate expression used for validation.</param>
	/// <param name="paramName">The name of the parameter (optional).</param>
	/// <param name="customException">A custom exception to throw if the condition is met (optional).</param>
	/// <returns>The validated value if the condition is not satisfied.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="predicate" /> is <c>null</c>.</exception>
	/// <exception cref="ArgumentException">Thrown if the predicate evaluates to true for <paramref name="value" />.</exception>
	/// <remarks>
	///     Compiled expressions are cached for repeated invocations with the same predicate structure.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ThrowIf<T>(
		this T                    value,
		Expression<Func<T, bool>> predicate,
		string?                   paramName       = null,
		Exception?                customException = null)
	{
		predicate.ThrowIfNull();

		var key      = new ExpressionCacheKey(typeof(T), predicate.ToString());
		var compiled = (Func<T, bool>)ExpressionCache.GetOrAdd(key, _ => predicate.Compile());

		if (!compiled(value)) return value;

		if (customException is { })
			throw customException;

		string name          = paramName ?? typeof(T).Name;
		var    conditionText = predicate.Body.ToString();
		var    msg           = $"Condition '{conditionText}' failed for parameter '{name}' with value '{value}'.";
		throw new ArgumentException(msg, name);
	}

	/// <summary>
	///     Throws an <see cref="ArgumentException" /> if the boolean <paramref name="condition" /> is <c>true</c>.
	/// </summary>
	/// <typeparam name="T">The type of value being checked.</typeparam>
	/// <param name="value">The value being checked.</param>
	/// <param name="condition">If set to <c>true</c>, the exception is thrown.</param>
	/// <param name="paramName">Name of the parameter related to the exception.</param>
	/// <param name="message">An optional custom message for the exception.</param>
	/// <param name="conditionExpr">Caller argument expression for <paramref name="condition" /> (automatically generated).</param>
	/// <param name="valueExpr">Caller argument expression for <paramref name="value" /> (automatically generated).</param>
	/// <returns>The value if <paramref name="condition" /> is <c>false</c>.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="condition" /> is <c>true</c>.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ThrowIf<T>(
		this T                                          value,
		bool                                            condition,
		string?                                         paramName     = null,
		string?                                         message       = null,
		[CallerArgumentExpression("condition")] string? conditionExpr = null,
		[CallerArgumentExpression("value")]     string? valueExpr     = null)
	{
		if (condition)
		{
			string name = paramName ?? valueExpr ?? typeof(T).Name;
			string msg  = message ?? $"Condition '{conditionExpr}' failed for parameter '{name}' with value '{value}'.";
			throw new ArgumentException(msg, name);
		}

		return value;
	}

	/// <summary>
	///     Throws an <see cref="ArgumentException" /> if the sequence is empty.
	/// </summary>
	/// <typeparam name="T">The type of sequence, must be <see cref="IEnumerable" />.</typeparam>
	/// <param name="sequence">The sequence to test for emptiness.</param>
	/// <param name="paramName">Name of the parameter (optional).</param>
	/// <returns>The non-empty <paramref name="sequence" />.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="sequence" /> is <c>null</c>.</exception>
	/// <exception cref="ArgumentException">Thrown if <paramref name="sequence" /> is empty.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ThrowIfEmpty<T>(
		this                                   T       sequence,
		[CallerArgumentExpression("sequence")] string? paramName = null)
		where T : IEnumerable
	{
		sequence.ThrowIfNull(paramName);

		if (sequence.HasAny()) return sequence;

		if (sequence is ICollection)
			ThrowEmptyCollection(paramName);
		else
			ThrowEmptySequence(paramName);

		return default!; // Unreachable
	}

	/// <summary>
	///     Throws an <see cref="ArgumentNullException" /> if <paramref name="value" /> is <see langword="null" />.
	///     The method returns the non-null value, thus enabling fluent use.
	/// </summary>
	/// <typeparam name="T">The type of <paramref name="value" />.</typeparam>
	/// <param name="value">The value to check for null.</param>
	/// <param name="paramName">
	///     The name of the parameter. Automatically captured via <see cref="CallerArgumentExpressionAttribute" />.
	/// </param>
	/// <returns>The non-null value.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="value" /> is <c>null</c>.</exception>
	/// <example>
	///     <code>
	/// var nameLength = user.Name.ThrowIfNull().Length;
	/// </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ThrowIfNull<T>(
		[NotNull] this T? value,
		[CallerArgumentExpression(nameof(value))]
		string? paramName = null)
	{
		if (value is { }) return value;

		ThrowArgumentNull(paramName);
		return default!; // Unreachable
	}

	[DoesNotReturn]
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void ThrowArgumentNull(string? paramName) =>
		throw new ArgumentNullException(paramName);

	[DoesNotReturn]
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void ThrowEmptyCollection(string? paramName) =>
		throw new EmptyCollectionException(paramName ?? "collection");

	[DoesNotReturn]
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void ThrowEmptySequence(string? paramName) =>
		throw new ArgumentException("Sequence cannot be empty.", paramName);

	private readonly record struct ExpressionCacheKey(Type Type, string ExpressionString);
}
