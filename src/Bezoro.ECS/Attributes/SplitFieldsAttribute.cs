namespace Bezoro.ECS.Attributes;

/// <summary>
///     Marks a component as eligible for source-generated split-group storage helpers.
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class SplitFieldsAttribute : Attribute { }
