using Bezoro.ECS.Attributes;
using Bezoro.GameSystems.ActivationSystem.Types;

namespace Bezoro.GameSystems.ActivationSystem.Services;

[Query]
[With(typeof(ActivationEntry))]
internal readonly partial struct ActivationEntryQuery;
