using System;

namespace Bezoro.Core.Common.Extensions;

public static class ActionExtensions
{
	public static void SafeInvoke(this Action? handler)
	{
		if (handler == null) return;

		foreach (var d in handler.GetInvocationList())
			try
			{
				((Action)d)();
			}
			catch (Exception ex)
			{
				Logger.LogException(ex);
			}
	}
}
