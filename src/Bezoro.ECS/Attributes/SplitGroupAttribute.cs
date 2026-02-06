namespace Bezoro.ECS.Attributes;

/// <summary>
///     Assigns a field to a split-storage group.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class SplitGroupAttribute(int groupId) : Attribute
{
	/// <summary>
	///     Gets the split group identifier.
	/// </summary>
	public int GroupId { get; } = groupId;
}
