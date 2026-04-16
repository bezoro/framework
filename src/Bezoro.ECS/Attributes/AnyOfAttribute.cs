namespace Bezoro.ECS.Attributes;

[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class AnyOfAttribute(Type componentType) : Attribute
{
	public Type ComponentType { get; } = componentType ?? throw new ArgumentNullException(nameof(componentType));
}
