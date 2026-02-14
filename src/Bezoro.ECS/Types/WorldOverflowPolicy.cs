namespace Bezoro.ECS.Types;

/// <summary>
/// Defines overflow handling behavior for fixed-capacity world buffers.
/// </summary>
public enum WorldOverflowPolicy
{
	/// <summary>
	/// Throws immediately when a configured capacity is exceeded.
	/// </summary>
	FailFast = 0,

	/// <summary>
	/// Drops the newest item when capacity is exceeded.
	/// </summary>
	DropNewest = 1
}
