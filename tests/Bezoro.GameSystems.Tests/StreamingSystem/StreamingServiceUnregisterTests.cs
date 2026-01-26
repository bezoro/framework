using System;
using System.Numerics;
using Bezoro.GameSystems.StreamingSystem.Services;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.StreamingSystem;

[TestSubject(typeof(StreamingService))]
public class StreamingServiceUnregisterTests
{
	[Fact]
	public void WhenEntityDoesNotExist_ShouldNotThrow()
	{
		using var system = new StreamingService();
		var       entity = new TestEntity(1, Vector3.Zero);

		var act = () => system.Unregister(entity);

		act.Should().NotThrow();
	}

	[Fact]
	public void WhenEntityExists_ShouldDecrementEntityCount()
	{
		using var system = new StreamingService();
		var       entity = new TestEntity(1, Vector3.Zero);
		system.Register(entity);

		system.Unregister(entity);

		system.EntityCount.Should().Be(0);
	}

	[Fact]
	public void WhenEntityIsNull_ShouldThrow()
	{
		using var system = new StreamingService();

		var act = () => system.Unregister(null!);

		act.Should().Throw<ArgumentNullException>().WithParameterName("entity");
	}
}
