using Bezoro.ECS.Abstractions;

namespace Bezoro.ECS.Attributes;

/// <summary>
///     Orders a system before another system type within the same execution stage.
/// </summary>
/// <param name="systemType">System type that must execute later.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = true)]
public sealed class BeforeAttribute(Type systemType) : Attribute
{
	/// <summary>
	///     Gets the system type that must execute later.
	/// </summary>
	public Type SystemType { get; } = systemType ?? throw new ArgumentNullException(nameof(systemType));
}
