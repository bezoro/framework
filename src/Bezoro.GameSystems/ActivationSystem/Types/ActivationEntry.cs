using System;

namespace Bezoro.GameSystems.ActivationSystem.Types;

/// <summary>
///     Internal mutable struct representing an activation entry's full state.
///     Stored in the service's ConcurrentDictionary.
/// </summary>
internal record struct ActivationEntry(
	Action           Callback,
	int              Priority,
	ActivationState  State
);
