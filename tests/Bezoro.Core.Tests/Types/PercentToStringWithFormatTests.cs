using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(Percent))]
public class PercentToStringWithFormatTests
{
#if NET6_0_OR_GREATER
	[Fact]
	public void WhenCalled_ShouldReturnPercentString()
	{
		var p = new Percent(42);

		var result = p.ToString(null, null);

		result.Should().Be("42%");
	}
#endif
}
