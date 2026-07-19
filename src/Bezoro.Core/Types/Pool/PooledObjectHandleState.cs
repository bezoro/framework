using System.Threading;
using Bezoro.Core.Abstractions;

namespace Bezoro.Core.Types.Pool;

internal sealed class PooledObjectHandleState<T>(T value, IPool<T> pool) where T : class
{
	private IPool<T>? _pool  = pool;
	private T?        _value = value;

	internal T? DebuggerValue => Volatile.Read(ref _value);

	internal bool IsDisposed => Volatile.Read(ref _value) is null;

	internal T Value => Volatile.Read(ref _value) ?? throw new ObjectDisposedException(nameof(PooledObjectHandle<T>));

	internal void Dispose()
	{
		var returnedValue = Interlocked.Exchange(ref _value, null);
		if (returnedValue is null)
			return;

		var pool = Interlocked.Exchange(ref _pool, null);
		pool?.Return(returnedValue);
	}
}
