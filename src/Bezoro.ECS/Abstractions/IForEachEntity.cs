using Bezoro.ECS.Types;

namespace Bezoro.ECS.Abstractions;

/// <summary>
///     Defines a job-style iterator over two component columns with entity access.
/// </summary>
/// <typeparam name="T1">Writable component type.</typeparam>
/// <typeparam name="T2">Read-only component type.</typeparam>
public interface IForEachEntity<T1, T2>
	where T1 : struct
	where T2 : struct
{
	/// <summary>
	///     Executes once per matched entity.
	/// </summary>
	/// <param name="entity">Matched entity.</param>
	/// <param name="component1">Writable component.</param>
	/// <param name="component2">Read-only component.</param>
	void Execute(Entity entity, ref T1 component1, in T2 component2);
}
