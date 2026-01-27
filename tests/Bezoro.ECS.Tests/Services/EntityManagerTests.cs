using Bezoro.ECS.Services;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(EntityManager))]
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
		entity3.Id.Should().Be(2);
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
		entity1.Id.Should().NotBe(entity2.Id);
	}

	[Fact]
	public void DestroyEntity_Should_Recycle_Entity_Id()
	{
		// Arrange
		var entityManager   = new EntityManager();
		var entityToDestroy = entityManager.CreateEntity();
		int originalId      = entityToDestroy.Id;

		// Act
		entityManager.DestroyEntity(entityToDestroy);
		var newEntity = entityManager.CreateEntity();

		// Assert
		newEntity.Id.Should().Be(originalId);
	}
}
