namespace Bezoro.ECS.Attributes;

[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
public sealed class NoneAttribute<T> : Attribute where T : struct;
