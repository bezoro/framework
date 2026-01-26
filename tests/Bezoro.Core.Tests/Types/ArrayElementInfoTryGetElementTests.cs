using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(ArrayElementInfo<>))]
public class ArrayElementInfoTryGetElementTests
{
	[Fact]
	public void WhenFound_ShouldReturnTrueAndSetOutParams()
	{
		var info = ArrayElementInfo<string>.Found(2, "hello", 10);

		bool result = info.TryGetElement(out string? element, out uint index);

		result.Should().BeTrue();
		element.Should().Be("hello");
		index.Should().Be(2u);
	}

	[Fact]
	public void WhenFoundWithValueType_ShouldReturnTrueAndSetOutParams()
	{
		var info = ArrayElementInfo<int>.Found(5, 42, 10);

		bool result = info.TryGetElement(out int element, out uint index);

		result.Should().BeTrue();
		element.Should().Be(42);
		index.Should().Be(5u);
	}

	[Fact]
	public void WhenNotFound_ShouldReturnFalseAndSetDefaults()
	{
		var info = ArrayElementInfo<string>.NotFound("missing", 10);

		bool result = info.TryGetElement(out string? element, out uint index);

		result.Should().BeFalse();
		element.Should().BeNull();
		index.Should().Be(0u);
	}
}
