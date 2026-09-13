namespace Bezoro.ECS.Attributes;

/// <summary>
///     Requires a query to match entities containing at least one of two component types.
/// </summary>
/// <param name="firstComponentType">First alternative component type.</param>
/// <param name="secondComponentType">Second alternative component type.</param>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
public sealed class AnyAttribute(Type firstComponentType, Type secondComponentType) : Attribute
{
	/// <summary>
	///     Gets the first alternative component type.
	/// </summary>
	public Type FirstComponentType { get; } =
		firstComponentType ?? throw new ArgumentNullException(nameof(firstComponentType));

	/// <summary>
	///     Gets the second alternative component type.
	/// </summary>
	public Type SecondComponentType { get; } =
		secondComponentType ?? throw new ArgumentNullException(nameof(secondComponentType));
}
