using Xunit.Abstractions;

namespace Bezoro.Chess.Tests;

public abstract class TestBase
{
	protected readonly ITestOutputHelper Output;

	protected TestBase(ITestOutputHelper output)
	{
		Output = output;
	}
}
