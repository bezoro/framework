using Bezoro.ECS.Abstractions;

namespace Bezoro.ECS.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = true)]
public sealed class AfterAttribute<TSystem> : Attribute where TSystem : ISystem;
