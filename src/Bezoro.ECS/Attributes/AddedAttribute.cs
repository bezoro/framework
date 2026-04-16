namespace Bezoro.ECS.Attributes;

[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
public sealed class AddedAttribute(Type componentType) : Attribute
{
	public Type ComponentType { get; } = componentType ?? throw new ArgumentNullException(nameof(componentType));
}
