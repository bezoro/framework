using System.Diagnostics;
using System.Runtime.CompilerServices;
using Bezoro.Core.Abstractions;

namespace Bezoro.Core.Types.Pool;

/// <summary>
///     A disposable handle that automatically returns a pooled object when disposed.
///     Designed for use with <c>using</c> statements to ensure objects are always returned.
/// </summary>
/// <typeparam name="T">The type of the pooled object.</typeparam>
/// <remarks>
///     This is a mutable struct to support safe double-dispose handling via atomic exchange.
///     Copies of the struct will have independent disposal state.
/// </remarks>
[DebuggerDisplay("Value={_value}, IsDisposed={_pool == null}")]
public struct PooledObjectHandle<T> : IDisposable where T : class
{
	private IPool<T>? _pool;
	private T?        _value;

	/// <summary>
	///     Initializes a new instance of the <see cref="PooledObjectHandle{T}" /> struct.
	/// </summary>
	/// <param name="value">The pooled object.</param>
	/// <param name="pool">The pool that owns the object.</param>
	internal PooledObjectHandle(T value, IPool<T> pool)
	{
		_value = value;
		_pool  = pool;
	}

	/// <summary>
	///     Gets whether this handle has been disposed.
	/// </summary>
	public readonly bool IsDisposed => _value is null;

	/// <summary>
	///     Gets the pooled object.
	/// </summary>
	/// <exception cref="ObjectDisposedException">Thrown if accessed after disposal.</exception>
	public readonly T Value
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _value ?? throw new ObjectDisposedException(nameof(PooledObjectHandle<T>));
	}

	/// <summary>
	///     Implicitly converts the handle to the underlying value.
	/// </summary>
	/// <param name="handle">The handle to convert.</param>
	/// <returns>The underlying pooled object.</returns>
	public static implicit operator T(PooledObjectHandle<T> handle) => handle.Value;

	/// <summary>
	///     Returns the object to the pool. Safe to call multiple times.
	/// </summary>
	public void Dispose()
	{
		var value = Interlocked.Exchange(ref _value, null);
		if (value is null)
			return;

		var pool = Interlocked.Exchange(ref _pool, null);
		pool?.Return(value);
	}
}
