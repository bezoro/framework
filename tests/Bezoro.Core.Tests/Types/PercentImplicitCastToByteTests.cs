using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(Percent))]
public class PercentImplicitCastToByteTests
{
	[Fact]
	public void WhenCast_WhenCalled_ShouldReturnValue()
	{
		var p = new Percent(42);

		byte value = p;

		value.Should().Be(42);
	}
}
