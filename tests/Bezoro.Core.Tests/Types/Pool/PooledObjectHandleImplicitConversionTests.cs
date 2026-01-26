using System.Text;
using Bezoro.Core.Types.Pool;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(PooledObjectHandle<>))]
public class PooledObjectHandleImplicitConversionTests
{
	[Fact]
	public void ShouldConvertToUnderlyingType()
	{
		// Use StringBuilder (not object) to properly test implicit conversion
		// Assigning to 'object' boxes the struct instead of triggering the operator
		var       pool          = new ObjectPool<StringBuilder>(() => new());
		using var handle        = pool.RentHandle();
		var       expectedValue = handle.Value;

		StringBuilder value = handle;

		value.Should().NotBeNull();
		value.Should().BeSameAs(expectedValue);
	}
}
