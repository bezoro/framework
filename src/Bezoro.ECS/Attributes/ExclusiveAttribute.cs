namespace Bezoro.ECS.Attributes;

/// <summary>
///     Marks a system as requiring exclusive access to the world while it executes.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class ExclusiveAttribute : Attribute;
