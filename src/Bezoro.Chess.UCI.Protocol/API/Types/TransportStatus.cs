namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents the current status of a UCI transport.
/// </summary>
public enum TransportStatus
{
	/// <summary>
	///     The transport has been constructed but not yet started.
	/// </summary>
	Created   = 0,

	/// <summary>
	///     The transport is in the process of starting.
	/// </summary>
	Starting  = 1,

	/// <summary>
	///     The transport is started and available for reads and writes.
	/// </summary>
	Started   = 2,

	/// <summary>
	///     The transport is shutting down.
	/// </summary>
	Stopping  = 3,

	/// <summary>
	///     The transport was stopped without being disposed.
	/// </summary>
	Stopped   = 4,

	/// <summary>
	///     The transport is being disposed.
	/// </summary>
	Disposing = 5,

	/// <summary>
	///     The transport has been fully disposed.
	/// </summary>
	Disposed  = 6,

	/// <summary>
	///     The transport encountered a failure during start or teardown.
	/// </summary>
	Failed    = 7,

	/// <summary>
	///     The transport operation was canceled.
	/// </summary>
	Canceled  = 8
}
