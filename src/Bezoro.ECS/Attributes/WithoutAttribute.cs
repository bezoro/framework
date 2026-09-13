namespace Bezoro.ECS.Attributes;

/// <summary>
///     Requires a generated query to exclude entities containing the specified component type.
/// </summary>
/// <param name="componentType">Excluded component type.</param>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class WithoutAttribute(Type componentType) : Attribute
{
	/// <summary>
	///     Gets the excluded component type.
	/// </summary>
	public Type ComponentType { get; } = componentType ?? throw new ArgumentNullException(nameof(componentType));
}
