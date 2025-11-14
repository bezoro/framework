namespace Bezoro.UCI.Domain;

/// <summary>
/// Represents the current status of a UCI transport.
/// </summary>
internal enum TransportStatus
{
	Created   = 0,
	Starting  = 1,
	Started   = 2,
	Stopping  = 3,
	Stopped   = 4,
	Disposing = 5,
	Disposed  = 6,
	Failed    = 7,
	Canceled  = 8
}

