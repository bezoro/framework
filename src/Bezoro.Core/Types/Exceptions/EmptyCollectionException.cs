namespace Bezoro.Core.Types.Exceptions;

/// <summary>
///     Represents an error that occurs when a collection is empty.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="EmptyCollectionException" /> class.
/// </remarks>
/// <param name="collectionName">The name of the collection that is empty.</param>
public class EmptyCollectionException(string collectionName) : Exception(
	string.IsNullOrWhiteSpace(collectionName)
		? "The collection is empty."
		: $"The collection '{collectionName}' is empty."
);
