using Bezoro.ECS.Types;

namespace Bezoro.ECS.Abstractions;

/// <summary>
/// Defines a job-style iterator over two component columns.
/// </summary>
/// <typeparam name="T1">Writable component type.</typeparam>
/// <typeparam name="T2">Read-only component type.</typeparam>
public interface IForEach<T1, T2>
	where T1 : struct, IComponent
	where T2 : struct, IComponent
{
	/// <summary>
	/// Executes once per matched entity.
	/// </summary>
	/// <param name="component1">Writable component.</param>
	/// <param name="component2">Read-only component.</param>
	void Execute(ref T1 component1, in T2 component2);
}
