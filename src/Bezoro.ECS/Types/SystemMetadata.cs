namespace Bezoro.ECS.Types;

/// <summary>
///     Generated or static metadata for a system type.
/// </summary>
public readonly struct SystemMetadata(
	Type   systemType,
	Type[] reads,
	Type[] writes,
	Type[] readResources,
	Type[] writeResources,
	bool   isExclusive)
{
	public bool IsExclusive { get; } = isExclusive;

	public Type   SystemType      { get; } = systemType ?? throw new ArgumentNullException(nameof(systemType));
	public Type[] Reads           { get; } = reads;
	public Type[] Writes          { get; } = writes;
	public Type[] ReadResources   { get; } = readResources;
	public Type[] WriteResources  { get; } = writeResources;
}
