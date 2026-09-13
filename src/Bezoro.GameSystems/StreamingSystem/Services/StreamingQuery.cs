using Bezoro.ECS.Attributes;
using Bezoro.GameSystems.MovementSystem.Types;
using Bezoro.GameSystems.StreamingSystem.Types;

namespace Bezoro.GameSystems.StreamingSystem.Services;

[Query]
[With(typeof(Position))]
[With(typeof(StreamState))]
internal readonly partial struct StreamingQuery;
