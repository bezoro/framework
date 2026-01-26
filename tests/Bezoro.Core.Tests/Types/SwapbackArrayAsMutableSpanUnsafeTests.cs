using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayAsMutableSpanUnsafeTests
{
	[Fact]
	public void WhenCalled_ShouldNotIncrementVersion()
	{
		var  arr            = new SwapbackArray<int> { 1, 2, 3 };
		uint initialVersion = arr.Version;

		var span = arr.AsMutableSpanUnsafe();
		span[0] = 99;

		arr.Version.Should().Be(initialVersion);
	}

	[Fact]
	public void WhenCalled_ShouldReturnWritableSpan()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3 };

		var span = arr.AsMutableSpanUnsafe();
		span[1] = 99;

		arr[1].Should().Be(99);
	}
}
