using System;

namespace Bezoro.Core.Common.Extensions;

public static class ActionExtensions
{
	public static void SafeInvoke(this Action handler)
	{
		handler.ThrowIfNull();

		foreach (var d in handler.GetInvocationList())
			((Action)d)?.Invoke();
	}
}
