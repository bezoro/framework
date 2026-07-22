namespace Bezoro.ECS.Attributes;

/// <summary>
///     Assigns a system to a logical system set used for scheduling and run conditions.
/// </summary>
/// <param name="setType">Type identifying the system set.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = true)]
public sealed class SystemSetAttribute(Type setType) : Attribute
{
	/// <summary>
	///     Gets the type identifying the system set.
	/// </summary>
	public Type SetType { get; } = setType ?? throw new ArgumentNullException(nameof(setType));
}
