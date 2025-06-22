using Xunit.Abstractions;

namespace Bezoro.Chess.Tests.Unit;

public abstract class TestBase
{
	protected readonly ITestOutputHelper Output;

	protected TestBase(ITestOutputHelper output)
	{
		Output = output;
	}
}
