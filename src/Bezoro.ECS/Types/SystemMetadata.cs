namespace Bezoro.ECS.Types;

/// <summary>
///     Generated or static metadata for a system type.
/// </summary>
/// <param name="systemType">Concrete system type described by the metadata.</param>
/// <param name="reads">Component types read by the system.</param>
/// <param name="writes">Component types written by the system.</param>
/// <param name="readResources">Resource types read by the system.</param>
/// <param name="writeResources">Resource types written by the system.</param>
/// <param name="isExclusive">Whether the system requires exclusive world access.</param>
public readonly struct SystemMetadata(
	Type   systemType,
	Type[] reads,
	Type[] writes,
	Type[] readResources,
	Type[] writeResources,
	bool   isExclusive)
{
	/// <summary>
	///     Gets whether the system requires exclusive world access.
	/// </summary>
	public bool IsExclusive { get; } = isExclusive;

	/// <summary>Gets the concrete system type.</summary>
	public Type   SystemType      { get; } = systemType ?? throw new ArgumentNullException(nameof(systemType));
	/// <summary>Gets the component types read by the system.</summary>
	public Type[] Reads           { get; } = reads;
	/// <summary>Gets the component types written by the system.</summary>
	public Type[] Writes          { get; } = writes;
	/// <summary>Gets the resource types read by the system.</summary>
	public Type[] ReadResources   { get; } = readResources;
	/// <summary>Gets the resource types written by the system.</summary>
	public Type[] WriteResources  { get; } = writeResources;
}
