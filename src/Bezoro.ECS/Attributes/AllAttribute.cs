namespace Bezoro.ECS.Attributes;

/// <summary>
///     Requires a query to match entities containing the specified component type.
/// </summary>
/// <param name="componentType">Required component type.</param>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
public sealed class AllAttribute(Type componentType) : Attribute
{
	/// <summary>
	///     Gets the required component type.
	/// </summary>
	public Type ComponentType { get; } = componentType ?? throw new ArgumentNullException(nameof(componentType));
}
