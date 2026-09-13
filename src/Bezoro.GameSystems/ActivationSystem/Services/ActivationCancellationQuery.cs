using Bezoro.ECS.Attributes;
using Bezoro.GameSystems.ActivationSystem.Types;

namespace Bezoro.GameSystems.ActivationSystem.Services;

[Query]
[With(typeof(ActivationCancellationRequest))]
internal readonly partial struct ActivationCancellationQuery;
