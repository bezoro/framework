using Bezoro.ECS.Attributes;
using Bezoro.GameSystems.TimerSystem.Types;

namespace Bezoro.GameSystems.TimerSystem.Services;

[Query]
[With(typeof(Timer))]
internal readonly partial struct TimerQuery;
