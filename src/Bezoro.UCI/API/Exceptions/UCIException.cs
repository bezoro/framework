namespace Bezoro.UCI.API.Exceptions
{
	/// <summary>
	///     A custom exception for errors related to the UCI protocol or engine communication.
	/// </summary>
	public class UCIException : Exception
	{
		public UCIException(string message) : base(message) { }
		public UCIException(string message, Exception innerException) : base(message, innerException) { }
	}
}
