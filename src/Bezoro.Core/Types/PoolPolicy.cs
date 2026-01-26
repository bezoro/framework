using System.Runtime.CompilerServices;
using Bezoro.Core.Abstractions;
using Bezoro.Core.Extensions;

namespace Bezoro.Core.Types;

/// <summary>
///     A flexible, delegate-based pool policy for common pooling scenarios.
/// </summary>
/// <typeparam name="T">The type of objects managed by the policy.</typeparam>
public sealed class PoolPolicy<T> : IPoolPolicy<T> where T : class
{
	private readonly Action<T>?     _onDiscard;
	private readonly Func<T, bool>? _reset;
	private readonly Func<T, bool>? _validate;
	private readonly Func<T>        _factory;

	/// <summary>
	///     Creates a policy with the specified factory and optional lifecycle delegates.
	/// </summary>
	/// <param name="factory">Factory to create new instances.</param>
	/// <param name="reset">Optional reset delegate returning <c>true</c> if the object is reusable.</param>
	/// <param name="validate">Optional validation delegate returning <c>true</c> if the object is valid.</param>
	/// <param name="onDiscard">Optional callback invoked when an object is discarded.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="factory" /> is <c>null</c>.</exception>
	public PoolPolicy(
		Func<T>        factory,
		Func<T, bool>? reset     = null,
		Func<T, bool>? validate  = null,
		Action<T>?     onDiscard = null)
	{
		_factory   = factory.ThrowIfNull();
		_reset     = reset;
		_validate  = validate;
		_onDiscard = onDiscard;
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public T Create() => _factory();

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Reset(T item)
	{
		if (item is IPooledObject pooled)
			return pooled.OnReturn();

		return _reset?.Invoke(item) ?? true;
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Validate(T item) => _validate?.Invoke(item) ?? true;

	/// <inheritdoc />
	public void OnDiscard(T item)
	{
		_onDiscard?.Invoke(item);

		if (item is IDisposable disposable)
			disposable.Dispose();
	}
}
