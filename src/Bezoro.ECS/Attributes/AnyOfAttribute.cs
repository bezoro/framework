namespace Bezoro.ECS.Attributes;

/// <summary>
///     Adds a component type to the alternatives accepted by a generated query specification.
/// </summary>
/// <param name="componentType">Alternative component type.</param>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class AnyOfAttribute(Type componentType) : Attribute
{
	/// <summary>
	///     Gets the alternative component type.
	/// </summary>
	public Type ComponentType { get; } = componentType ?? throw new ArgumentNullException(nameof(componentType));
}
