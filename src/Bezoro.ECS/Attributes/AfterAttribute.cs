using Bezoro.ECS.Abstractions;

namespace Bezoro.ECS.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = true)]
public sealed class AfterAttribute(Type systemType) : Attribute
{
	public Type SystemType { get; } = systemType ?? throw new ArgumentNullException(nameof(systemType));
}
