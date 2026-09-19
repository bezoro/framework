using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Core.Abstractions;
using Bezoro.Core.Types;
using Bezoro.Core.Types.Pool;

namespace Bezoro.Core.Tests.Types.Pool;

internal sealed class LegacyClearPool<T> : IPool<T> where T : class
{
	public int AvailableCount => 0;

	public IReadOnlyList<bool> ClearArguments => _clearArguments;

	public int MaxCapacity => -1;

	public PoolStatistics Statistics => default;

	public int TotalCount => 0;

	private readonly List<bool> _clearArguments = [];

	public void Clear(bool disposeItems) => _clearArguments.Add(disposeItems);

	public PooledObjectHandle<T> RentHandle() => throw new NotSupportedException();

	public T Rent() => throw new NotSupportedException();

	public ValueTask<T> RentAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

	public ValueTask<T?> RentAsync(TimeSpan timeout, CancellationToken cancellationToken = default) => throw new NotSupportedException();

	public bool Return(T item) => false;

	public int TrimExcess(Percent targetUtilization = default) => 0;

	public bool TryRent([NotNullWhen(true)] out T? item)
	{
		item = null;
		return false;
	}
}
