namespace Bezoro.ECS.Types;

/// <summary>
///     Represents a callback invoked when an existing component is set during command playback.
/// </summary>
/// <typeparam name="T">The component type.</typeparam>
/// <param name="entity">The entity whose component was updated.</param>
/// <param name="component">A reference to the updated component value.</param>
public delegate void OnSetObserver<T>(Entity entity, ref T component)
	where T : struct;
