using System.Collections.Generic;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using JetBrains.Annotations;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(World))]
public partial class WorldRuntimeTests
{
	private struct Acceleration
	{
		public float X;
		public float Y;
	}

	private readonly struct AddedPositionQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder) => builder.Added<Position>();
	}

	private readonly struct AnyPositionOrVelocityQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder)
		{
			builder.Any<Position>();
			builder.Any<Velocity>();
		}
	}

	private readonly struct ChangedPositionQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder) => builder.Changed<Position>();
	}

	private struct Follows;

	private struct IntegrateJob(float dt) : IForEach<Position, Velocity>
	{
		public void Execute(ref Position component1, in Velocity component2)
		{
			component1.X += component2.X * dt;
			component1.Y += component2.Y * dt;
		}
	}

	private struct RecordingEntityIntegrateJob(List<int> order) : IForEachEntity<Position, Velocity>
	{
		private readonly List<int> _order = order;

		public void Execute(Entity entity, ref Position component1, in Velocity component2)
		{
			_order.Add((int)component1.X);
			component1.Y += component2.Y;
		}
	}

	private struct RecordingIntegrateJob(List<int> order) : IForEach<Position, Velocity>
	{
		private readonly List<int> _order = order;

		public void Execute(ref Position component1, in Velocity component2)
		{
			_order.Add((int)component1.X);
			component1.Y += component2.Y;
		}
	}

	private struct RecordingPositionJob(List<int> order) : IForEach<Position>
	{
		private readonly List<int> _order = order;

		public void Execute(ref Position component1)
		{
			_order.Add((int)component1.X);
			component1.Y += 100;
		}
	}

	private struct RecordingEntityPositionJob(List<int> order) : IForEachEntity<Position>
	{
		private readonly List<int> _order = order;

		public void Execute(Entity entity, ref Position component1)
		{
			_order.Add((int)component1.X);
			component1.Y += 100;
		}
	}

	private struct ManagedTag
	{
		public Payload? Payload;
	}

	private readonly struct ManagedTagQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder) => builder.All<ManagedTag>();
	}

	private sealed record Payload(string Name);

	private struct Position
	{
		public float X;
		public float Y;
	}

	private readonly struct PositionAndVelocityQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder)
		{
			builder.All<Position>();
			builder.All<Velocity>();
		}
	}

	private readonly struct PositionQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder) => builder.All<Position>();
	}

	private readonly struct PositionRelatedToAnyFollowsQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder)
		{
			builder.All<Position>();
			builder.Related<Follows>();
		}
	}

	private readonly struct PositionRelatedToEntityZeroQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder)
		{
			builder.All<Position>();
			builder.Related<Follows>(new(0, 0));
		}
	}

	private readonly struct PositionVelocityAccelerationQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder)
		{
			builder.All<Position>();
			builder.All<Velocity>();
			builder.All<Acceleration>();
		}
	}

	private readonly struct PositionVelocityAccelerationScaleQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder)
		{
			builder.All<Position>();
			builder.All<Velocity>();
			builder.All<Acceleration>();
			builder.All<Scale>();
		}
	}

	private readonly struct PositionWithOptionalVelocityQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder)
		{
			builder.All<Position>();
			builder.Optional<Velocity>();
		}
	}

	private readonly struct PositionWithoutVelocityQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder)
		{
			builder.All<Position>();
			builder.None<Velocity>();
		}
	}

	private struct Scale
	{
		public float Value;
	}

	private struct Velocity
	{
		public float X;
		public float Y;
	}
}
