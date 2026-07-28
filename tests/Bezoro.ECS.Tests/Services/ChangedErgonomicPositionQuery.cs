using Bezoro.ECS.Attributes;

namespace Bezoro.ECS.Tests.Services;

[Query]
[Changed(typeof(ErgonomicPosition))]
internal readonly partial struct ChangedErgonomicPositionQuery;
