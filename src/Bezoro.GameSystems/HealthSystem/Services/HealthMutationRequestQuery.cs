using Bezoro.ECS.Attributes;
using Bezoro.GameSystems.HealthSystem.Types;

namespace Bezoro.GameSystems.HealthSystem.Services;

[Query]
[With(typeof(HealthMutationRequest))]
internal readonly partial struct HealthMutationRequestQuery;
