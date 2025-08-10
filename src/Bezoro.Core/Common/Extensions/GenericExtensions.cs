using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Bezoro.Core.Common.Extensions;

/// <summary>
///     A collection of frequently-used helper methods that extend the capabilities of any type <typeparamref name="T" />.
///     All members are implemented as pure functions (no side-effects) and therefore can be safely used in
///     LINQ queries, unit-tests or anywhere short, expressive code is preferred.
/// </summary>
public static class GenericExtensions
{
	/// <summary>
	///     Returns <see langword="true" /> when <paramref name="value" /> is greater than or equal to
	///     <paramref name="min" /> and less than or equal to <paramref name="max" />.
	/// </summary>
	/// <exception cref="ArgumentException">
	///     Thrown when <paramref name="min" /> is greater than <paramref name="max" />.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsBetween<T>(this T value, T min, T max) where T : IComparable<T>
	{
		if (value == null) throw new ArgumentNullException(nameof(value));
		if (min   == null) throw new ArgumentNullException(nameof(min));
		if (max   == null) throw new ArgumentNullException(nameof(max));

		return value.CompareTo(min) >= 0 && value.CompareTo(max) <= 0;
	}

	/// <summary>
	///     Determines whether <paramref name="value" /> equals the default value for its type.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsDefault<T>(this T value) where T : struct =>
		EqualityComparer<T>.Default.Equals(value, default);

	/// <summary>
	///     Logical negation of <see cref="IsDefault{T}(T)" />.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsNotDefault<T>(this T value) where T : struct => !value.IsDefault();

	/// <summary>
	///     Logical negation of <see cref="IsNull{T}(T?)" />.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsNotNull<T>([NotNullWhen(true)] this T? value) => value is not null;

	/// <summary>
	///     Determines whether a <see langword="nullable" /> value is <see langword="null" />.
	/// </summary>
	/// <remarks>
	///     An unconstrained generic <typeparamref name="T" /> is used so that both reference and value-types wrapped in
	///     <c>Nullable&lt;T&gt;</c> are supported.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsNull<T>([NotNullWhen(false)] this T? value) => value is null;

	/// <summary>
	///     Determines whether <paramref name="value" /> is equal to any element of <paramref name="candidates" />.
	/// </summary>
	/// <example>
	///     <code>
	/// if (statusCode.IsOneOf(200, 201, 202)) …
	/// </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsOneOf<T>(this T value, params T[] candidates)
	{
		if (value      == null) throw new ArgumentNullException(nameof(value));
		if (candidates == null) throw new ArgumentNullException(nameof(candidates));

		return candidates.Length != 0 && candidates.Contains(value);
	}

	/// <summary>
	///     Converts a single item into an <see cref="IEnumerable{T}" /> containing that item.
	///     Handy for feeding single elements into APIs that expect an enumerable.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static IEnumerable<T> Yield<T>(this T item)
	{
		if (item == null) throw new ArgumentNullException(nameof(item));

		yield return item;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ThrowIf<T>(
		this T                    value,
		Expression<Func<T, bool>> predicate,
		string?                   paramName = null)
	{
		predicate.ThrowIfNull();

		var compiled = predicate.Compile();

		if (!compiled(value)) return value;

		string name          = paramName ?? typeof(T).Name;
		var    conditionText = predicate.Body.ToString();
		var    msg           = $"Condition '{conditionText}' failed for parameter '{name}' with value '{value}'.";
		throw new ArgumentException(msg, name);
	}

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
	///     Throws <see cref="ArgumentNullException" /> when <paramref name="value" /> is <see langword="null" />.
	///     The method returns the non-null value thus enabling fluent usage:
	///     <code>
	/// var nameLength = user.Name.ThrowIfNull().Length;
	/// </code>
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T ThrowIfNull<T>([NotNull] this T? value, string? paramName = null)
	{
		if (value is null) throw new ArgumentNullException(paramName ?? typeof(T).Name);

		return value;
	}
}
