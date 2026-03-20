namespace Bezoro.Chess.UCI.Protocol.API.Abstractions;

/// <summary>
///     Abstraction for tapping into UCI output line streams.
/// </summary>
public interface IUciLineSource
{
	/// <summary>
	///     Subscribes to output lines produced by the engine. Returns an <see cref="IDisposable" /> for unsubscribing.
	/// </summary>
	IDisposable Subscribe(Action<string> handler);
}
