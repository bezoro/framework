namespace Bezoro.ECS.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = true)]
public sealed class SystemSetAttribute<TSet> : Attribute;
