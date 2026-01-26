using System;
using System.Numerics;
using Bezoro.GameSystems.StreamingSystem.Services;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.StreamingSystem;

[TestSubject(typeof(StreamingService))]
public class StreamingServiceRegisterTests
{
	[Fact]
	public void WhenDuplicateEntityId_ShouldNotDuplicate()
	{
		using var system  = new StreamingService();
		var       entity1 = new TestEntity(1, Vector3.Zero);
		var       entity2 = new TestEntity(1, Vector3.One);

		system.Register(entity1);
		system.Register(entity2);

		system.EntityCount.Should().Be(1);
	}

	[Fact]
	public void WhenEntityIsNull_ShouldThrow()
	{
		using var system = new StreamingService();

		var act = () => system.Register(null!);

		act.Should().Throw<ArgumentNullException>().WithParameterName("entity");
	}

	[Fact]
	public void WhenValidEntity_ShouldIncrementEntityCount()
	{
		using var system = new StreamingService();
		var       entity = new TestEntity(1, Vector3.Zero);

		system.Register(entity);

		system.EntityCount.Should().Be(1);
	}
}
