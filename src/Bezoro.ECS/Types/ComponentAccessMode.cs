namespace Bezoro.ECS.Types;

/// <summary>
///     Describes how a system accesses a component type.
/// </summary>
public enum ComponentAccessMode
{
	/// <summary>
	///     The component is read-only.
	/// </summary>
	ReadOnly,

	/// <summary>
	///     The component is read and written.
	/// </summary>
	ReadWrite
}
