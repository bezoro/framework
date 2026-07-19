namespace Bezoro.ECS.Attributes;

/// <summary>
///     Declares that a system may mutate a world resource.
/// </summary>
/// <param name="resourceType">Resource type written by the system.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = true)]
public sealed class WritesResourceAttribute(Type resourceType) : Attribute
{
	/// <summary>
	///     Gets the resource type written by the system.
	/// </summary>
	public Type ResourceType { get; } = resourceType ?? throw new ArgumentNullException(nameof(resourceType));
}
