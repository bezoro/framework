namespace Bezoro.ECS.Attributes;

/// <summary>
///     Declares a component that a generated query may read when present without requiring it for a match.
/// </summary>
/// <param name="componentType">Optional component type.</param>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
public sealed class OptionalAttribute(Type componentType) : Attribute
{
	/// <summary>
	///     Gets the optional component type.
	/// </summary>
	public Type ComponentType { get; } = componentType ?? throw new ArgumentNullException(nameof(componentType));
}
