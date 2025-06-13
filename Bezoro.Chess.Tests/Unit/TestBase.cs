using Xunit.Abstractions;

namespace Bezoro.Chess.Tests.Unit;

public abstract class TestBase
{
	protected TestBase(ITestOutputHelper output)
	{
		Output = output;
	}

	protected readonly ITestOutputHelper Output;
}
