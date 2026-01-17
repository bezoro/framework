using Bezoro.ECS.Types;
using Xunit;

namespace Bezoro.ECS.Tests.Types;

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
		Assert.Equal(expectedId, entity.Id);
	}
}
