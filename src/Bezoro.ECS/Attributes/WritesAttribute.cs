namespace Bezoro.ECS.Attributes;

/// <summary>
///     Declares that a system may mutate a component type.
/// </summary>
/// <param name="componentType">Component type written by the system.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class WritesAttribute(Type componentType) : Attribute
{
	/// <summary>
	///     Gets the component type written by the system.
	/// </summary>
	public Type ComponentType { get; } = componentType ?? throw new ArgumentNullException(nameof(componentType));
}
