using System;

namespace Bezoro.GameSystems.ActivationSystem.Types;

internal readonly struct ActivationCommand
{
	private ActivationCommand(
		ActivationCommandKind kind,
		ActivationHandle      handle,
		Action?               callback,
		int                   priority)
	{
		Kind = kind;
		Handle = handle;
		Callback = callback;
		Priority = priority;
	}

	public ActivationCommandKind Kind { get; }

	public ActivationHandle Handle { get; }

	public Action? Callback { get; }

	public int Priority { get; }

	public static ActivationCommand Register(ActivationHandle handle, Action callback, int priority) =>
		new(ActivationCommandKind.Register, handle, callback, priority);

	public static ActivationCommand Cancel(ActivationHandle handle) =>
		new(ActivationCommandKind.Cancel, handle, null, 0);
}
