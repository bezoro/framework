namespace Bezoro.ECS.Attributes;

[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class AnyOfAttribute<T> : Attribute where T : struct;
