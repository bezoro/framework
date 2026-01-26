using System;

namespace Bezoro.GameSystems.DamageSystem.Types;

/// <summary>
///     Optional flags that describe how damage should behave.
/// </summary>
[Flags]
public enum DamageFlags : byte
{
	None     = 0,
	Critical = 1 << 0,
	True     = 1 << 1
}
