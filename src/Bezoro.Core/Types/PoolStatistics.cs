using System.Diagnostics;

namespace Bezoro.Core.Types;

/// <summary>
///     Diagnostic statistics for a pool instance.
/// </summary>
[DebuggerDisplay("Rented={TotalRented}, Returned={TotalReturned}, Created={TotalCreated}")]
public readonly record struct PoolStatistics
{
	/// <summary>
	///     Total number of async wait operations performed.
	/// </summary>
	public long TotalAsyncWaits { get; init; }

	/// <summary>
	///     Total number of objects created (factory invocations).
	/// </summary>
	public long TotalCreated { get; init; }

	/// <summary>
	///     Total number of objects discarded (failed validation or reset).
	/// </summary>
	public long TotalDiscarded { get; init; }

	/// <summary>
	///     Total number of objects rented since pool creation.
	/// </summary>
	public long TotalRented { get; init; }

	/// <summary>
	///     Total number of objects returned since pool creation.
	/// </summary>
	public long TotalReturned { get; init; }

	/// <summary>
	///     Total number of async wait timeouts.
	/// </summary>
	public long TotalTimeouts { get; init; }

	/// <summary>
	///     Current utilization percentage (rented / total).
	/// </summary>
	public Percent Utilization { get; init; }
}
