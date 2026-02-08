namespace Bezoro.ECS.Attributes;

[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
public sealed class AnyAttribute<T1, T2> : Attribute
	where T1 : struct
	where T2 : struct { }
