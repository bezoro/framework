namespace Bezoro.ECS.Internal.V3;

internal readonly record struct SystemAccessMetadata(int[] Reads, int[] Writes, bool IsExclusive);
