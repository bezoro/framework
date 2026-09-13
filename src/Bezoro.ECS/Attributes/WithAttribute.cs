namespace Bezoro.ECS.Attributes;

/// <summary>
///     Requires a generated query to match entities containing the specified component type.
/// </summary>
/// <param name="componentType">Required component type.</param>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class WithAttribute(Type componentType) : Attribute
{
	/// <summary>
	///     Gets the required component type.
	/// </summary>
	public Type ComponentType { get; } = componentType ?? throw new ArgumentNullException(nameof(componentType));
}
