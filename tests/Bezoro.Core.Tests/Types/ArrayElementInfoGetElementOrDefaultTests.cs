using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(ArrayElementInfo<>))]
public class ArrayElementInfoGetElementOrDefaultTests
{
	[Fact]
	public void WhenFound_WhenCalled_ShouldReturnElement()
	{
		var info = ArrayElementInfo<string>.Found(0, "hello", 5);

		info.GetElementOrDefault().Should().Be("hello");
	}

	[Fact]
	public void WhenNotFound_WhenCalled_ShouldReturnDefault()
	{
		var info = ArrayElementInfo<int>.NotFound(42, 5);

		info.GetElementOrDefault().Should().Be(0);
	}

	[Fact]
	public void WithDefaultValue_WhenFound_ShouldReturnElement()
	{
		var info = ArrayElementInfo<string>.Found(0, "hello", 5);

		info.GetElementOrDefault("fallback").Should().Be("hello");
	}

	[Fact]
	public void WithDefaultValue_WhenFoundButElementIsNull_ShouldReturnNull()
	{
		var info = ArrayElementInfo<string>.Found(0, null!, 5);

		info.GetElementOrDefault("fallback").Should().BeNull();
	}

	[Fact]
	public void WithDefaultValue_WhenNotFound_ShouldReturnDefaultValue()
	{
		var info = ArrayElementInfo<string>.NotFound(null, 5);

		info.GetElementOrDefault("fallback").Should().Be("fallback");
	}
}
