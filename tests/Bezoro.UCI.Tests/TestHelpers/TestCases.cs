namespace Bezoro.UCI.Tests.TestHelpers;

/// <summary>
/// Test case record for channel capacity validation tests.
/// </summary>
/// <param name="Value">The channel capacity value to test.</param>
/// <param name="Description">A human-readable description of the test case.</param>
public record ChannelCapacityTestCase(int Value, string Description)
{
	public override string ToString() => Description;
}

/// <summary>
/// Test case record for timeout-related tests.
/// </summary>
/// <param name="Timeout">The timeout value to test.</param>
/// <param name="ShouldSucceed">Whether the operation should succeed with this timeout.</param>
/// <param name="Description">A human-readable description of the test case.</param>
public record TimeoutTestCase(TimeSpan Timeout, bool ShouldSucceed, string Description)
{
	public override string ToString() => Description;
}

/// <summary>
/// Test case record for FEN string validation tests.
/// </summary>
/// <param name="Fen">The FEN string to test.</param>
/// <param name="IsValid">Whether the FEN string should be considered valid.</param>
/// <param name="Description">A human-readable description of the test case.</param>
public record FenTestCase(string Fen, bool IsValid, string Description)
{
	public override string ToString() => Description;
}

/// <summary>
/// Test case record for process argument tests.
/// </summary>
/// <param name="Arguments">The arguments to pass to the process.</param>
/// <param name="ShouldSucceed">Whether the process should start successfully.</param>
/// <param name="Description">A human-readable description of the test case.</param>
public record ProcessArgumentsTestCase(string[]? Arguments, bool ShouldSucceed, string Description)
{
	public override string ToString() => Description;
}

/// <summary>
/// Test case record for move score tests.
/// </summary>
/// <param name="ScoreCp">The centipawn score, or null.</param>
/// <param name="ScoreMate">The mate score, or null.</param>
/// <param name="Description">A human-readable description of the test case.</param>
public record MoveScoreTestCase(int? ScoreCp, int? ScoreMate, string Description)
{
	public override string ToString() => Description;
}

/// <summary>
/// Test case record for encoding tests.
/// </summary>
/// <param name="EncodingName">The name of the encoding to use.</param>
/// <param name="TestString">The string to test with the encoding.</param>
/// <param name="Description">A human-readable description of the test case.</param>
public record EncodingTestCase(string EncodingName, string TestString, string Description)
{
	public override string ToString() => Description;
}

/// <summary>
/// Test case record for command validation tests.
/// </summary>
/// <param name="Command">The command to validate.</param>
/// <param name="IsValid">Whether the command should be considered valid.</param>
/// <param name="Description">A human-readable description of the test case.</param>
public record CommandValidationTestCase(string Command, bool IsValid, string Description)
{
	public override string ToString() => Description;
}
