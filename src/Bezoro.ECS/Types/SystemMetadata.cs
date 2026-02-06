namespace Bezoro.ECS.Types;

/// <summary>
///     Generated or static metadata for a system type.
/// </summary>
public readonly struct SystemMetadata
{
	public SystemMetadata(Type systemType, Type[] reads, Type[] writes, bool isExclusive)
	{
		SystemType  = systemType ?? throw new ArgumentNullException(nameof(systemType));
		Reads       = reads;
		Writes      = writes;
		IsExclusive = isExclusive;
	}

	public bool IsExclusive { get; }

	public Type   SystemType { get; }
	public Type[] Reads      { get; }
	public Type[] Writes     { get; }
}
