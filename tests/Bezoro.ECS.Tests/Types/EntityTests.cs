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
		const int expectedId = 123;

		// Act
		var entity = new Entity(expectedId);

		// Assert
		entity.Id.Should().Be(expectedId);
	}
}
