namespace Bezoro.ECS.Types;

/// <summary>
///     Observer callback invoked before a component is removed.
/// </summary>
public delegate void OnRemoveObserver<T>(Entity entity, in T component)
	where T : struct;
