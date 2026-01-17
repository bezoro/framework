namespace Bezoro.Core.Types.Exceptions;

/// <summary>
///     Represents an error that occurs when a collection is empty.
/// </summary>
public class EmptyCollectionException : Exception
{
	/// <summary>
	///     Initializes a new instance of the <see cref="EmptyCollectionException" /> class.
	/// </summary>
	/// <param name="collectionName">The name of the collection that is empty.</param>
	public EmptyCollectionException(string collectionName) : base(
		string.IsNullOrWhiteSpace(collectionName)
			? "The collection is empty."
			: $"The collection '{collectionName}' is empty.") { }
}
