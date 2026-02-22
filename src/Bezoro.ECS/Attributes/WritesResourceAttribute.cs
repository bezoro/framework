namespace Bezoro.ECS.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = true)]
public sealed class WritesResourceAttribute<TResource> : Attribute;
