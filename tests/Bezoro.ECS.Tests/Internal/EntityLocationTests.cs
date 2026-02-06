using Bezoro.ECS.Internal;
using FluentAssertions;
using JetBrains.Annotations;
using System.Runtime.InteropServices;
using Xunit;

namespace Bezoro.ECS.Tests.Internal;

[TestSubject(typeof(EntityLocation))]
public class EntityLocationTests
{
	[Fact]
	public void Empty_WhenUsed_ShouldBeInvalid()
	{
		EntityLocation.Empty.IsValid.Should().BeFalse();
	}

	[Fact]
	public void Constructor_WhenValuesProvided_ShouldStoreArchetypeAndRow()
	{
		var location = new EntityLocation(7, 133);

		location.ArchetypeId.Should().Be(7);
		location.RowIndex.Should().Be(133);
	}

	[Fact]
	public void EntityLocation_ShouldBeEightBytes()
	{
		Marshal.SizeOf<EntityLocation>().Should().Be(8);
	}
}
