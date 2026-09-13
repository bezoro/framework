namespace Bezoro.Core.Helpers;

/// <summary>
///     Exception thrown when an array is full and cannot accept more items.
/// </summary>
public sealed class ArrayIsFullException : Exception
{
	/// <summary>
	///     Initializes a new instance of the <see cref="ArrayIsFullException" /> class with a standard error message.
	/// </summary>
	public ArrayIsFullException()
		: base("The array is full and cannot accept more items.") { }
}
