namespace Bezoro.ECS.Attributes;

/// <summary>
///     Marks a partial structure as a source-generated ECS query specification.
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class QueryAttribute : Attribute;
