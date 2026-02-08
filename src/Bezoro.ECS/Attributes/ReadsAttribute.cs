namespace Bezoro.ECS.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class ReadsAttribute<T> : Attribute where T : struct { }
