using Bezoro.ECS.Abstractions;

namespace Bezoro.ECS.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class WritesAttribute<T> : Attribute where T : struct, IComponent
{
}
