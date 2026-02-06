using Bezoro.ECS.Abstractions;

namespace Bezoro.ECS.Attributes;

[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
public sealed class AllAttribute<T> : Attribute where T : struct, IComponent { }
