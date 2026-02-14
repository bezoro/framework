using System;
using System.Runtime.InteropServices;
using Bezoro.ECS.Options;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(WorldV1))]
public class ChunkCapacitySizingTests
{
	[Fact]
	public void ArchetypeChunkCapacity_WhenChunkSizeBytesConfigured_ShouldUseByteBudget()
	{
		var world = new WorldV1(
			new WorldOptions
			{
				ChunkSizeInBytes = 64
			}
		);

		var archetype        = world.GetOrCreateArchetype(typeof(SizedPosition));
		int elementSize      = Marshal.SizeOf(typeof(SizedPosition));
		int entitySize       = Marshal.SizeOf(typeof(Entity));
		int expectedCapacity = Math.Max(1, 64 / (entitySize + elementSize));

		archetype.ChunkCapacity.Should().Be(expectedCapacity);
	}

	[Fact]
	public void ArchetypeChunkCapacity_WhenExplicitChunkCapacityConfigured_ShouldOverrideByteBudget()
	{
		var world = new WorldV1(
			new WorldOptions
			{
				ChunkSizeInBytes = 64,
				ChunkCapacity    = 3
			}
		);

		var archetype = world.GetOrCreateArchetype(typeof(SizedPosition));

		archetype.ChunkCapacity.Should().Be(3);
	}

	[Fact]
	public void ArchetypeChunkCapacity_WhenNoComponents_ShouldUseByteBudgetWithEntitySize()
	{
		var world = new WorldV1(
			new WorldOptions
			{
				ChunkSizeInBytes = 64
			}
		);

		int entitySize       = Marshal.SizeOf(typeof(Entity));
		int expectedCapacity = Math.Max(1, 64 / entitySize);

		world.GetOrCreateArchetype().ChunkCapacity.Should().Be(expectedCapacity);
	}

	private struct SizedPosition	{
		public float X { get; init; }
		public float Y { get; init; }
	}
}
