namespace Bezoro.ECS.Attributes;

/// <summary>
///     Requires a query to exclude entities containing the specified component type.
/// </summary>
/// <param name="componentType">Excluded component type.</param>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
public sealed class NoneAttribute(Type componentType) : Attribute
{
	/// <summary>
	///     Gets the excluded component type.
	/// </summary>
	public Type ComponentType { get; } = componentType ?? throw new ArgumentNullException(nameof(componentType));
}
