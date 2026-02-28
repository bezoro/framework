namespace Bezoro.ECS.Attributes;

[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public sealed class WithoutAttribute<T> : Attribute where T : struct;
