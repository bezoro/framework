namespace Bezoro.ECS.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class ReadsAttribute(Type componentType) : Attribute
{
	public Type ComponentType { get; } = componentType ?? throw new ArgumentNullException(nameof(componentType));
}
