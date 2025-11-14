using System.IO;
using System.Text;

namespace Bezoro.UCI.Domain.Common.Helpers;

/// <summary>
/// Holds references to the streams used for process communication.
/// </summary>
internal sealed class ProcessStreams
{
	public StreamWriter? Stdin  { get; set; }
	public StreamReader? Stdout { get; set; }
	public StreamReader? Stderr { get; set; }
}

