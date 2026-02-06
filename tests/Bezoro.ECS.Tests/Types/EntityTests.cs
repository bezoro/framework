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
		const int EXPECTED_ID      = 123;
		const int EXPECTED_VERSION = 4;

		// Act
		var entity = new Entity(EXPECTED_ID, EXPECTED_VERSION);

		// Assert
		entity.Id.Should().Be(EXPECTED_ID);
		entity.Version.Should().Be(EXPECTED_VERSION);
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
