using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayAddUncheckedTests
{
	[Fact]
	public void ShouldIncrementVersion()
	{
		var  arr            = new SwapbackArray<int>(10);
		uint initialVersion = arr.Version;

		arr.AddUnchecked(1);
		uint finalVersion = arr.Version;

		arr.Version.Should().NotBe(initialVersion);
		arr.Version.Should().Be(finalVersion);
	}

	[Fact]
	public void WhenSuccessful_ShouldAppendItem()
	{
		var arr = new SwapbackArray<int>(10);

		arr.AddUnchecked(42);

		arr[0].Should().Be(42);
		arr.Count.Should().Be(1);
	}
}
