namespace Bezoro.ECS.Abstractions;

/// <summary>
///     Defines a job-style iterator over one writable component column.
/// </summary>
/// <typeparam name="T1">Writable component type.</typeparam>
public interface IForEach<T1>
	where T1 : struct
{
	/// <summary>
	///     Executes once per matched entity.
	/// </summary>
	/// <param name="component1">Writable component.</param>
	void Execute(ref T1 component1);
}
