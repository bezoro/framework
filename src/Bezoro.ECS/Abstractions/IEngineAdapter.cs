using Bezoro.ECS.Types;

namespace Bezoro.ECS.Abstractions;

/// <summary>
/// Adapter used to bridge world data to an engine runtime.
/// </summary>
public interface IEngineAdapter
{
    void SyncTransformsToEngine(ReadOnlySpan<Entity> entities, ReadOnlySpan<TransformProxy> transforms);
    InputStateProxy PollInput();
    float GetDeltaTime();
}
