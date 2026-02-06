using Bezoro.ECS.Abstractions;

namespace Bezoro.ECS.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class ReadsAttribute<T> : Attribute where T : struct, IComponent
{
}
