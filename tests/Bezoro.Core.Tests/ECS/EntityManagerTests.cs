using Bezoro.Core.ECS;
using Xunit;

namespace Bezoro.Core.Tests.ECS;

public class EntityManagerTests
{
	[Fact]
	public void CreateEntity_Should_Generate_New_Id_When_Pool_Is_Empty()
	{
		// Arrange
		var entityManager = new EntityManager();
		var entity1       = entityManager.CreateEntity(); // Id 0
		var entity2       = entityManager.CreateEntity(); // Id 1

		// Act
		var entity3 = entityManager.CreateEntity();

		// Assert
		Assert.Equal(2, entity3.Id);
	}

	[Fact]
	public void CreateEntity_Should_Return_Entities_With_Unique_Ids()
	{
		// Arrange
		var entityManager = new EntityManager();

		// Act
		var entity1 = entityManager.CreateEntity();
		var entity2 = entityManager.CreateEntity();

		// Assert
		Assert.NotEqual(entity1.Id, entity2.Id);
	}

	[Fact]
	public void DestroyEntity_Should_Recycle_Entity_Id()
	{
		// Arrange
		var entityManager   = new EntityManager();
		var entityToDestroy = entityManager.CreateEntity();
		var originalId      = entityToDestroy.Id;

		// Act
		entityManager.DestroyEntity(entityToDestroy);
		var newEntity = entityManager.CreateEntity();

		// Assert
		Assert.Equal(originalId, newEntity.Id);
	}
}
