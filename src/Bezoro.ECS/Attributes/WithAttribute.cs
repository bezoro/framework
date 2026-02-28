namespace Bezoro.ECS.Attributes;

[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class WithAttribute<T> : Attribute where T : struct;
