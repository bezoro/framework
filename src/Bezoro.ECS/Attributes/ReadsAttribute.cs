namespace Bezoro.ECS.Attributes;

/// <summary>
///     Declares that a system reads a component type without mutating it.
/// </summary>
/// <param name="componentType">Component type read by the system.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class ReadsAttribute(Type componentType) : Attribute
{
	/// <summary>
	///     Gets the component type read by the system.
	/// </summary>
	public Type ComponentType { get; } = componentType ?? throw new ArgumentNullException(nameof(componentType));
}
