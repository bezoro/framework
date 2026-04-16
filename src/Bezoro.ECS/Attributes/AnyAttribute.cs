namespace Bezoro.ECS.Attributes;

[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
public sealed class AnyAttribute(Type firstComponentType, Type secondComponentType) : Attribute
{
	public Type FirstComponentType { get; } =
		firstComponentType ?? throw new ArgumentNullException(nameof(firstComponentType));

	public Type SecondComponentType { get; } =
		secondComponentType ?? throw new ArgumentNullException(nameof(secondComponentType));
}
