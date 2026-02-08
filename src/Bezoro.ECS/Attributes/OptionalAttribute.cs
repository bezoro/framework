namespace Bezoro.ECS.Attributes;

[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
public sealed class OptionalAttribute<T> : Attribute where T : struct { }
