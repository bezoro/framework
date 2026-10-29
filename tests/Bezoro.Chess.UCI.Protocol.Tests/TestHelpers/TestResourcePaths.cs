namespace Bezoro.Chess.UCI.Protocol.Tests.TestHelpers;

/// <summary>
///     Test constants for file paths and external resources.
/// </summary>
public static class TestResourcePaths
{
	/// <summary>
	///     Directory containing test resources.
	/// </summary>
	public const string RESOURCES_DIRECTORY = "../../../TestResources";

	/// <summary>
	///     Directory containing the Stockfish engine.
	/// </summary>
	public const string STOCKFISH_DIRECTORY = RESOURCES_DIRECTORY + "/Engine/stockfish";

	/// <summary>
	///     Path to the Stockfish executable for integration tests.
	/// </summary>
	public static string STOCKFISH_PATH =>
		System.Environment.GetEnvironmentVariable("BEZORO_STOCKFISH_PATH") is { Length: > 0 } path
			? path
			: STOCKFISH_DIRECTORY + "/stockfish-windows-x86-64-avx2.exe";
}
