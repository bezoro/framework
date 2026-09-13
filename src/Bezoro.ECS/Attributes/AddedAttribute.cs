namespace Bezoro.ECS.Attributes;

/// <summary>
///     Requires a query to match entities where a component was added during the current change-tracking window.
/// </summary>
/// <param name="componentType">Component type whose additions should be matched.</param>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
public sealed class AddedAttribute(Type componentType) : Attribute
{
	/// <summary>
	///     Gets the component type whose additions should be matched.
	/// </summary>
	public Type ComponentType { get; } = componentType ?? throw new ArgumentNullException(nameof(componentType));
}
