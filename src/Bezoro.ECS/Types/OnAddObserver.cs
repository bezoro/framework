using Bezoro.ECS.Abstractions;

namespace Bezoro.ECS.Types;

/// <summary>
///     Observer callback invoked after a component is added or set.
/// </summary>
public delegate void OnAddObserver<T>(Entity entity, ref T component)
	where T : struct, IComponent;
