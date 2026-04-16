namespace Bezoro.ECS.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = true)]
public sealed class SystemSetAttribute(Type setType) : Attribute
{
	public Type SetType { get; } = setType ?? throw new ArgumentNullException(nameof(setType));
}
