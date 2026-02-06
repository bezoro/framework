using System.Runtime.InteropServices;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Types;

[TestSubject(typeof(Entity))]
public class EntityTests
{
	[Fact]
	public void Entity_Should_Store_Id()
	{
		// Arrange
		const int expectedId      = 123;
		const int expectedVersion = 4;

		// Act
		var entity = new Entity(expectedId, expectedVersion);

		// Assert
		entity.Id.Should().Be(expectedId);
		entity.Version.Should().Be(expectedVersion);
	}

	[Fact]
	public void Entity_ShouldBeEightBytes()
	{
		Marshal.SizeOf<Entity>().Should().Be(8);
	}

	[Fact]
	public void None_ShouldRepresentInvalidEntity()
	{
		Entity.None.Id.Should().Be(-1);
		Entity.None.Version.Should().Be(0);
	}
}
