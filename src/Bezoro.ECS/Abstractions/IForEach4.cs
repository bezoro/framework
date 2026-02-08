namespace Bezoro.ECS.Abstractions;

/// <summary>
///     Defines a job-style iterator over four component columns.
/// </summary>
/// <typeparam name="T1">Writable component type.</typeparam>
/// <typeparam name="T2">Read-only component type.</typeparam>
/// <typeparam name="T3">Read-only component type.</typeparam>
/// <typeparam name="T4">Read-only component type.</typeparam>
public interface IForEach<T1, T2, T3, T4>
	where T1 : struct
	where T2 : struct
	where T3 : struct
	where T4 : struct
{
	/// <summary>
	///     Executes once per matched entity.
	/// </summary>
	/// <param name="component1">Writable component.</param>
	/// <param name="component2">Read-only component.</param>
	/// <param name="component3">Read-only component.</param>
	/// <param name="component4">Read-only component.</param>
	void Execute(ref T1 component1, in T2 component2, in T3 component3, in T4 component4);
}
