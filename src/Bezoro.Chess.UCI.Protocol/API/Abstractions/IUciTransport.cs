using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Bezoro.Chess.UCI.Protocol.API.Abstractions;

/// <summary>
///     Abstraction for UCI engine transport.
///     Threading: unless otherwise noted, callbacks and events are invoked on ThreadPool threads.
/// </summary>
public interface IUciTransport : IAsyncDisposable, IDisposable
{
	/// <summary>
	///     Best-effort health indicator.
	///     True when started, the underlying process hasn't exited, and background loops are still running.
	/// </summary>
	bool IsHealthy { get; }

	/// <summary>
	///     Gets a value indicating whether the transport has been started and not yet fully stopped or disposed.
	/// </summary>
	bool IsStarted { get; }

	/// <summary>
	///     Current transport status. This reflects internal lifecycle transitions.
	/// </summary>
	TransportStatus Status { get; }

	/// <summary>
	///     Asynchronously reads lines from the transport stream.
	/// </summary>
	/// <param name="ct">Optional cancellation token to cancel the operation.</param>
	/// <returns>An async enumerable sequence of strings representing lines read from the transport.</returns>
	IAsyncEnumerable<string> ReadLinesAsync(CancellationToken ct = default);

	/// <summary>
	///     Starts the transport asynchronously.
	/// </summary>
	/// <param name="ct">Optional cancellation token to cancel the operation.</param>
	/// <returns>A task representing the asynchronous start operation.</returns>
	Task StartAsync(CancellationToken ct = default);

	/// <summary>
	///     Stops the transport asynchronously.
	/// </summary>
	/// <param name="ct">Optional cancellation token to cancel the operation.</param>
	/// <returns>A task representing the asynchronous stop operation.</returns>
	Task StopAsync(CancellationToken ct = default);

	/// <summary>
	///     Writes a line to the transport asynchronously.
	/// </summary>
	/// <param name="line">The line to write.</param>
	/// <param name="ct">Optional cancellation token to cancel the operation.</param>
	/// <returns>A task representing the asynchronous write operation.</returns>
	Task WriteLineAsync(string line, CancellationToken ct = default);

	/// <summary>
	///     Attempts to write a line to the transport asynchronously with a timeout.
	/// </summary>
	/// <param name="line">The line to write.</param>
	/// <param name="timeout">The maximum time to wait for the write operation.</param>
	/// <param name="ct">Optional cancellation token to cancel the operation.</param>
	/// <returns>A task that resolves to true if the write succeeded within the timeout, false otherwise.</returns>
	Task<bool> TryWriteLineAsync(string line, TimeSpan timeout, CancellationToken ct = default);

	/// <summary>
	///     Raised when an internal error occurs.
	///     Threading: invoked on a ThreadPool thread; exceptions thrown by handlers are swallowed.
	/// </summary>
	event Action<Exception>? Error;

	/// <summary>
	///     Raised when the engine process exits.
	///     Threading: invoked on a ThreadPool thread; exceptions thrown by handlers are swallowed.
	/// </summary>
	event Action<int?, string?>? Exited;

	/// <summary>
	///     Raised when a line is received on stderr (if redirected).
	///     Threading: invoked on a ThreadPool thread; exceptions thrown by handlers are swallowed.
	/// </summary>
	event Action<string>? StderrReceived;
}
