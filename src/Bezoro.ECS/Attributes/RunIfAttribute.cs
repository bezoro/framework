using Bezoro.ECS.Abstractions;

namespace Bezoro.ECS.Attributes;

/// <summary>
///     Applies a run condition to a system or system set.
/// </summary>
/// <param name="runConditionType">Type implementing <see cref="ISystemRunCondition" />.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = true)]
public sealed class RunIfAttribute(Type runConditionType) : Attribute
{
	/// <summary>
	///     Gets the run-condition type.
	/// </summary>
	public Type RunConditionType { get; } =
		runConditionType ?? throw new ArgumentNullException(nameof(runConditionType));
}
