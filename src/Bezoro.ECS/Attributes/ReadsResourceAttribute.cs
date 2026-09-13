namespace Bezoro.ECS.Attributes;

/// <summary>
///     Declares that a system reads a world resource without mutating it.
/// </summary>
/// <param name="resourceType">Resource type read by the system.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = true)]
public sealed class ReadsResourceAttribute(Type resourceType) : Attribute
{
	/// <summary>
	///     Gets the resource type read by the system.
	/// </summary>
	public Type ResourceType { get; } = resourceType ?? throw new ArgumentNullException(nameof(resourceType));
}
