namespace Bezoro.Core.Common.Extensions;

/// <summary>
///     Provides extension methods for safely invoking <see cref="Action" /> delegates.
/// </summary>
public static class ActionExtensions
{
    /// <summary>
	///     Safely invokes each delegate in the invocation list of the specified <see cref="Action" />, throwing an exception
	///     if the handler is <c>null</c>.
    /// </summary>
	/// <param name="handler">The <see cref="Action" /> delegate to invoke.</param>
	/// <exception cref="ArgumentNullException">Thrown if the <paramref name="handler" /> is <c>null</c>.</exception>
    public static void SafeInvoke(this Action handler)
	{
		handler.ThrowIfNull();

		foreach (var d in handler.GetInvocationList())
			((Action)d)?.Invoke();
	}
}
