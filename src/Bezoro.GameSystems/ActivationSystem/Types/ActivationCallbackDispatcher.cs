using System;

namespace Bezoro.GameSystems.ActivationSystem.Types;

/// <summary>
///     Dispatches an activation callback to a host-specific execution context.
/// </summary>
/// <param name="callback">Callback to dispatch.</param>
public delegate void ActivationCallbackDispatcher(Action callback);
