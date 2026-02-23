using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(ArrayElementInfo<>))]
public class ArrayElementInfoImplicitBoolConversionTests
{
	[Fact]
	public void WhenFound_WhenCalled_ShouldReturnTrue()
	{
		var info = ArrayElementInfo<int>.Found(0, 42, 5);

		bool result = info;

		result.Should().BeTrue();
	}

	[Fact]
	public void WhenNotFound_WhenCalled_ShouldReturnFalse()
	{
		var info = ArrayElementInfo<int>.NotFound(42, 5);

		bool result = info;

		result.Should().BeFalse();
	}

	[Fact]
	public void WhenUsedInIfStatement_WhenCalled_ShouldWork()
	{
		var found    = ArrayElementInfo<string>.Found(0, "test", 1);
		var notFound = ArrayElementInfo<string>.NotFound("missing", 5);

		var foundResult    = false;
		var notFoundResult = true;

		if (found)
			foundResult = true;

		if (notFound)
			notFoundResult = false;

		foundResult.Should().BeTrue();
		notFoundResult.Should().BeTrue();
	}
}
