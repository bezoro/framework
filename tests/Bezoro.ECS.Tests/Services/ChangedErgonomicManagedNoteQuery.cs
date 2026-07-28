using Bezoro.ECS.Attributes;

namespace Bezoro.ECS.Tests.Services;

[Query]
[Changed(typeof(ErgonomicManagedNote))]
internal readonly partial struct ChangedErgonomicManagedNoteQuery;
