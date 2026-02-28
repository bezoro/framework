using Bezoro.ECS.Types;

namespace Bezoro.ECS.Abstractions;

/// <summary>
///     Defines a job-style iterator over one writable component column with entity access.
/// </summary>
/// <typeparam name="T1">Writable component type.</typeparam>
public interface IForEachEntity<T1>
	where T1 : struct
{
	/// <summary>
	///     Executes once per matched entity.
	/// </summary>
	/// <param name="entity">Matched entity.</param>
	/// <param name="component1">Writable component.</param>
	void Execute(Entity entity, ref T1 component1);
}
