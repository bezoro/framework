using System.IO;

namespace Bezoro.UCI.Domain.Common.Helpers;

/// <summary>
///     Holds references to the streams used for process communication.
/// </summary>
internal sealed class ProcessStreams
{
	public StreamReader? Stderr { get; set; }
	public StreamReader? Stdout { get; set; }
	public StreamWriter? Stdin  { get; set; }
}
