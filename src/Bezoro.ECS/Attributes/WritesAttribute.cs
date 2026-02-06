using Bezoro.ECS.Abstractions;

namespace Bezoro.ECS.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class WritesAttribute<T> : Attribute where T : struct, IComponent { }
