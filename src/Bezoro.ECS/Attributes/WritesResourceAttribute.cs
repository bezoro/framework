namespace Bezoro.ECS.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = true)]
public sealed class WritesResourceAttribute(Type resourceType) : Attribute
{
	public Type ResourceType { get; } = resourceType ?? throw new ArgumentNullException(nameof(resourceType));
}
