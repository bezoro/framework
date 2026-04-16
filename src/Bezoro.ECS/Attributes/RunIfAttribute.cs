using Bezoro.ECS.Abstractions;

namespace Bezoro.ECS.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = true)]
public sealed class RunIfAttribute(Type runConditionType) : Attribute
{
	public Type RunConditionType { get; } =
		runConditionType ?? throw new ArgumentNullException(nameof(runConditionType));
}
