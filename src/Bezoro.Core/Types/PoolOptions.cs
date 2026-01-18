namespace Bezoro.Core.Types;

/// <summary>
///     Configuration options for object pool behavior.
/// </summary>
/// <param name="InitialCapacity">Initial number of pre-created objects. Default is 0.</param>
/// <param name="MaxCapacity">Maximum pool capacity. Use -1 for unbounded. Default is -1.</param>
/// <param name="ShrinkThreshold">Utilization threshold below which to consider shrinking. Default is 25%.</param>
/// <param name="EnableAsyncWait">Whether to allow async waiting when pool is exhausted. Default is <c>true</c>.</param>
/// <param name="AsyncWaitTimeout">Default timeout for async rent operations. Default is 30 seconds.</param>
/// <param name="ValidateOnRent">Whether to call Validate before returning rented objects. Default is <c>true</c>.</param>
/// <param name="TrackStatistics">Whether to enable detailed statistics collection. Default is <c>false</c>.</param>
public readonly record struct PoolOptions(
	int InitialCapacity = 0,
	int MaxCapacity = -1,
	Percent ShrinkThreshold = default,
	bool EnableAsyncWait = true,
	TimeSpan AsyncWaitTimeout = default,
	bool ValidateOnRent = true,
	bool TrackStatistics = false)
{
	/// <summary>
	///     Default options with sensible production defaults.
	/// </summary>
	public static PoolOptions Default => new(
		MaxCapacity: -1,
		ShrinkThreshold: Percent.Quarter,
		EnableAsyncWait: true,
		AsyncWaitTimeout: TimeSpan.FromSeconds(30),
		ValidateOnRent: true);

	/// <summary>
	///     Options optimized for high-throughput scenarios.
	/// </summary>
	public static PoolOptions HighThroughput => new(
		InitialCapacity: Environment.ProcessorCount * 2,
		MaxCapacity: Environment.ProcessorCount * 8,
		ValidateOnRent: false,
		TrackStatistics: false);
}
