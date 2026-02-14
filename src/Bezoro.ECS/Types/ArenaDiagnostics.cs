namespace Bezoro.ECS.Types;

/// <summary>
///     Immutable diagnostics for one fixed-capacity arena.
/// </summary>
public sealed class ArenaDiagnostics
{
	/// <summary>
	///     Creates a diagnostics snapshot.
	/// </summary>
	/// <param name="name">Arena name.</param>
	/// <param name="capacity">Configured capacity.</param>
	/// <param name="used">Current used slots.</param>
	/// <param name="highWatermark">Peak used slots observed.</param>
	/// <param name="overflowCount">Number of overflow attempts.</param>
	public ArenaDiagnostics(string name, int capacity, int used, int highWatermark, int overflowCount)
	{
		if (string.IsNullOrWhiteSpace(name))
			throw new ArgumentException("Arena name cannot be empty.", nameof(name));

		if (capacity < 0)
			throw new ArgumentOutOfRangeException(nameof(capacity));

		if (used < 0 || used > capacity)
			throw new ArgumentOutOfRangeException(nameof(used));

		if (highWatermark < 0 || highWatermark > capacity)
			throw new ArgumentOutOfRangeException(nameof(highWatermark));

		if (overflowCount < 0)
			throw new ArgumentOutOfRangeException(nameof(overflowCount));

		Name          = name;
		Capacity      = capacity;
		Used          = used;
		HighWatermark = highWatermark;
		OverflowCount = overflowCount;
	}

	/// <summary>
	///     Configured capacity.
	/// </summary>
	public int Capacity { get; }

	/// <summary>
	///     Peak used slots observed.
	/// </summary>
	public int HighWatermark { get; }

	/// <summary>
	///     Number of overflow attempts.
	/// </summary>
	public int OverflowCount { get; }

	/// <summary>
	///     Current used slots.
	/// </summary>
	public int Used { get; }

	/// <summary>
	///     Arena name.
	/// </summary>
	public string Name { get; }
}
