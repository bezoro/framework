namespace Bezoro.ECS.Attributes;

/// <summary>
///     Requires a query to match entities whose component changed during the current tracking window.
/// </summary>
/// <param name="componentType">Component type whose changes should be matched.</param>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
public sealed class ChangedAttribute(Type componentType) : Attribute
{
	/// <summary>
	///     Gets the component type whose changes should be matched.
	/// </summary>
	public Type ComponentType { get; } = componentType ?? throw new ArgumentNullException(nameof(componentType));
}
