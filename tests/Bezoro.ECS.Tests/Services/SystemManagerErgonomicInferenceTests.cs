using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Attributes;
using Bezoro.ECS.Options;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(SystemManager))]
public class SystemManagerErgonomicInferenceTests
{
	[Fact]
	public void UpdateAll_WhenSystemsUseExplicitResourceReadAndWriteApis_ShouldSerializeExecution()
	{
		using var world = new World(new WorldOptions { MaxDegreeOfParallelism = 4 });
		var       probe = new ErgonomicConcurrencyProbe();
		world.SetResource(new ErgonomicSchedulerResource());

		world.AddSystem(new ErgonomicReadResourceSystem(probe));
		world.AddSystem(new ErgonomicWriteResourceSystem(probe));

		world.Tick(1f / 60f);

		probe.MaxConcurrent.Should().Be(1);
	}

	[Fact]
	public void UpdateAll_WhenSystemsUseGeneratedEntityAwareQueryViewJobs_ShouldSerializeExecution()
	{
		using var world = new World(new WorldOptions { MaxDegreeOfParallelism = 4 });
		var       probe = new ErgonomicConcurrencyProbe();

		world.Spawn(
			new ErgonomicJobPosition { Value = 1 },
			new ErgonomicJobVelocity { Value = 2 }
		);

		world.AddSystem(new ErgonomicEntityAwareJobSystem(probe));
		world.AddSystem(new ErgonomicRegularJobSystem(probe));

		world.Tick(1f / 60f);

		probe.MaxConcurrent.Should().Be(1);
	}

	[Fact]
	public void UpdateAll_WhenSystemsUseReadOnlyResourceApisOnSameType_ShouldAllowConcurrentExecution()
	{
		using var world = new World(new WorldOptions { MaxDegreeOfParallelism = 4 });
		var       probe = new ErgonomicConcurrencyProbe();
		world.SetResource(new ErgonomicSchedulerResource());

		world.AddSystem(new ErgonomicReadResourceSystem(probe));
		world.AddSystem(new ErgonomicReadResourceSystemB(probe));

		world.Tick(1f / 60f);

		probe.MaxConcurrent.Should().Be(2);
	}

	[Fact]
	public void UpdateAll_WhenSystemsUseGeneratedEntityAwareWorldJobs_ShouldSerializeExecution()
	{
		using var world = new World(new WorldOptions { MaxDegreeOfParallelism = 4 });
		var       probe = new ErgonomicConcurrencyProbe();

		world.Spawn(
			new ErgonomicJobPosition { Value = 1 },
			new ErgonomicJobVelocity { Value = 2 }
		);

		world.AddSystem(new ErgonomicEntityAwareWorldJobSystem(probe));
		world.AddSystem(new ErgonomicRegularWorldJobSystem(probe));

		world.Tick(1f / 60f);

		probe.MaxConcurrent.Should().Be(1);
	}

	[Fact]
	public void UpdateAll_WhenSystemsUseGeneratedEntityAwareCursorJobs_ShouldSerializeExecution()
	{
		using var world = new World(new WorldOptions { MaxDegreeOfParallelism = 4 });
		var       probe = new ErgonomicConcurrencyProbe();

		world.Spawn(
			new ErgonomicJobPosition { Value = 1 },
			new ErgonomicJobVelocity { Value = 2 }
		);

		world.AddSystem(new ErgonomicEntityAwareCursorJobSystem(probe));
		world.AddSystem(new ErgonomicRegularCursorJobSystem(probe));

		world.Tick(1f / 60f);

		probe.MaxConcurrent.Should().Be(1);
	}

	[Fact]
	public void UpdateAll_WhenSystemsUseReadOnlyAndMutableQueryViewAccessOnSameManagedComponent_ShouldSerializeExecution()
	{
		using var world = new World(new WorldOptions { MaxDegreeOfParallelism = 4 });
		var       probe = new ErgonomicConcurrencyProbe();

		world.Spawn(new ErgonomicReadOnlyNote { Label = "ready" });

		world.AddSystem(new ErgonomicReadOnlyQueryViewSystemA(probe));
		world.AddSystem(new ErgonomicMutableQueryViewSystem(probe));

		world.Tick(1f / 60f);

		probe.MaxConcurrent.Should().Be(1);
	}

	[Fact]
	public void UpdateAll_WhenSystemsUseReadOnlyQueryViewForEachOnSameManagedComponent_ShouldAllowConcurrentExecution()
	{
		using var world = new World(new WorldOptions { MaxDegreeOfParallelism = 4 });
		var       probe = new ErgonomicConcurrencyProbe();

		world.Spawn(new ErgonomicReadOnlyNote { Label = "ready" });

		world.AddSystem(new ErgonomicReadOnlyQueryViewSystemA(probe));
		world.AddSystem(new ErgonomicReadOnlyQueryViewSystemB(probe));

		world.Tick(1f / 60f);

		probe.MaxConcurrent.Should().Be(2);
	}

	[Fact]
	public void UpdateAll_WhenSystemsUseWorldRunParallelOnDisjointComponents_ShouldAllowConcurrentExecution()
	{
		using var world = new World(new WorldOptions { MaxDegreeOfParallelism = 4 });
		var       probe = new ErgonomicConcurrencyProbe();

		world.Spawn(new ErgonomicParallelA { Value = 1 });
		world.Spawn(new ErgonomicParallelB { Value = 2 });

		world.AddSystem(new ErgonomicParallelWorldSystemA(probe));
		world.AddSystem(new ErgonomicParallelWorldSystemB(probe));

		world.Tick(1f / 60f);

		probe.MaxConcurrent.Should().Be(2);
	}
}

internal sealed class ErgonomicConcurrencyProbe
{
	private readonly System.Threading.ManualResetEventSlim _release = new(false);
	private          int                                   _maxConcurrent;
	private          int                                   _running;

	public int MaxConcurrent => System.Threading.Volatile.Read(ref _maxConcurrent);

	public void Enter()
	{
		int running = System.Threading.Interlocked.Increment(ref _running);
		UpdateMax(running);

		if (running == 1)
			_release.Wait(System.TimeSpan.FromMilliseconds(200));
		else
			_release.Set();

		System.Threading.Interlocked.Decrement(ref _running);
	}

	private void UpdateMax(int candidate)
	{
		while (true)
		{
			int current = System.Threading.Volatile.Read(ref _maxConcurrent);
			if (candidate <= current)
				return;

			if (System.Threading.Interlocked.CompareExchange(ref _maxConcurrent, candidate, current) == current)
				return;
		}
	}
}

internal sealed class ErgonomicReadResourceSystem(ErgonomicConcurrencyProbe probe) : ISystem
{
	public void Update(in SystemContext context)
	{
		_ = context.World.ReadResource<ErgonomicSchedulerResource>();
		probe.Enter();
	}
}

internal sealed class ErgonomicSchedulerResource;

internal sealed class ErgonomicReadResourceSystemB(ErgonomicConcurrencyProbe probe) : ISystem
{
	public void Update(in SystemContext context)
	{
		_ = context.World.ReadResource<ErgonomicSchedulerResource>();
		probe.Enter();
	}
}

internal sealed class ErgonomicWriteResourceSystem(ErgonomicConcurrencyProbe probe) : ISystem
{
	public void Update(in SystemContext context)
	{
		ref var resource = ref context.World.GetOrCreateResource<ErgonomicSchedulerResource>();
		probe.Enter();
	}
}

[Query]
[With<ErgonomicJobPosition>]
[With<ErgonomicJobVelocity>]
internal readonly partial struct ErgonomicJobQuery;

internal struct ErgonomicJobPosition
{
	public int Value;
}

internal struct ErgonomicJobVelocity
{
	public int Value;
}

internal readonly struct ErgonomicEntityAwareJob(ErgonomicConcurrencyProbe probe)
	: IForEachEntity<ErgonomicJobPosition, ErgonomicJobVelocity>
{
	public void Execute(Entity entity, ref ErgonomicJobPosition component1, in ErgonomicJobVelocity component2)
	{
		probe.Enter();
		component1.Value += component2.Value;
	}
}

internal readonly struct ErgonomicRegularJob(ErgonomicConcurrencyProbe probe)
	: IForEach<ErgonomicJobPosition, ErgonomicJobVelocity>
{
	public void Execute(ref ErgonomicJobPosition component1, in ErgonomicJobVelocity component2)
	{
		probe.Enter();
		component1.Value += component2.Value;
	}
}

internal sealed class ErgonomicEntityAwareJobSystem(ErgonomicConcurrencyProbe probe) : ISystem
{
	public void Update(in SystemContext context)
	{
		context.World.Query<ErgonomicJobQuery>().Run(new ErgonomicEntityAwareJob(probe));
	}
}

internal sealed class ErgonomicRegularJobSystem(ErgonomicConcurrencyProbe probe) : ISystem
{
	public void Update(in SystemContext context)
	{
		context.World.Query<ErgonomicJobQuery>().Run(new ErgonomicRegularJob(probe));
	}
}

internal sealed class ErgonomicEntityAwareWorldJobSystem(ErgonomicConcurrencyProbe probe) : ISystem
{
	public void Update(in SystemContext context)
	{
		var handle = context.World.Compile<ErgonomicJobQuery>();
		context.World.Run(handle, new ErgonomicEntityAwareJob(probe));
	}
}

internal sealed class ErgonomicRegularWorldJobSystem(ErgonomicConcurrencyProbe probe) : ISystem
{
	public void Update(in SystemContext context)
	{
		var handle = context.World.Compile<ErgonomicJobQuery>();
		context.World.Run(handle, new ErgonomicRegularJob(probe));
	}
}

internal sealed class ErgonomicEntityAwareCursorJobSystem(ErgonomicConcurrencyProbe probe) : ISystem
{
	public void Update(in SystemContext context)
	{
		var handle = context.World.Compile<ErgonomicJobQuery>();
		using var cursor = context.World.Execute(handle);
		if (!cursor.MoveNext())
			return;

		cursor.Run(new ErgonomicEntityAwareJob(probe));
	}
}

internal sealed class ErgonomicRegularCursorJobSystem(ErgonomicConcurrencyProbe probe) : ISystem
{
	public void Update(in SystemContext context)
	{
		var handle = context.World.Compile<ErgonomicJobQuery>();
		using var cursor = context.World.Execute(handle);
		if (!cursor.MoveNext())
			return;

		cursor.Run(new ErgonomicRegularJob(probe));
	}
}

[Query]
[With<ErgonomicReadOnlyNote>]
internal readonly partial struct ErgonomicReadOnlyNoteQuery;

internal struct ErgonomicReadOnlyNote
{
	public string Label;
}

internal sealed class ErgonomicReadOnlyQueryViewSystemA(ErgonomicConcurrencyProbe probe) : ISystem
{
	public void Update(in SystemContext context)
	{
		context.World.Query<ErgonomicReadOnlyNoteQuery>().ForEachRead<ErgonomicReadOnlyNote>(
			(Entity entity, in ErgonomicReadOnlyNote note) => probe.Enter()
		);
	}
}

internal sealed class ErgonomicReadOnlyQueryViewSystemB(ErgonomicConcurrencyProbe probe) : ISystem
{
	public void Update(in SystemContext context)
	{
		context.World.Query<ErgonomicReadOnlyNoteQuery>().ForEachRead<ErgonomicReadOnlyNote>(
			(Entity entity, in ErgonomicReadOnlyNote note) => probe.Enter()
		);
	}
}

internal sealed class ErgonomicMutableQueryViewSystem(ErgonomicConcurrencyProbe probe) : ISystem
{
	public void Update(in SystemContext context)
	{
		context.World.Query<ErgonomicReadOnlyNoteQuery>().ForEach<ErgonomicReadOnlyNote>(
			(Entity entity, ref ErgonomicReadOnlyNote note) =>
			{
				probe.Enter();
				note.Label += "!";
			}
		);
	}
}

[Query]
[With<ErgonomicParallelA>]
internal readonly partial struct ErgonomicParallelQueryA;

[Query]
[With<ErgonomicParallelB>]
internal readonly partial struct ErgonomicParallelQueryB;

internal struct ErgonomicParallelA
{
	public int Value;
}

internal struct ErgonomicParallelB
{
	public int Value;
}

internal readonly struct ErgonomicParallelJobA(ErgonomicConcurrencyProbe probe) : IForEach<ErgonomicParallelA>
{
	public void Execute(ref ErgonomicParallelA component1)
	{
		probe.Enter();
		component1.Value++;
	}
}

internal readonly struct ErgonomicParallelJobB(ErgonomicConcurrencyProbe probe) : IForEach<ErgonomicParallelB>
{
	public void Execute(ref ErgonomicParallelB component1)
	{
		probe.Enter();
		component1.Value++;
	}
}

internal sealed class ErgonomicParallelWorldSystemA(ErgonomicConcurrencyProbe probe) : ISystem
{
	public void Update(in SystemContext context)
	{
		var handle = context.World.Compile<ErgonomicParallelQueryA>();
		context.World.RunParallel<ErgonomicParallelQueryA, ErgonomicParallelJobA, ErgonomicParallelA>(
			handle,
			new(probe),
			2
		);
	}
}

internal sealed class ErgonomicParallelWorldSystemB(ErgonomicConcurrencyProbe probe) : ISystem
{
	public void Update(in SystemContext context)
	{
		var handle = context.World.Compile<ErgonomicParallelQueryB>();
		context.World.RunParallel<ErgonomicParallelQueryB, ErgonomicParallelJobB, ErgonomicParallelB>(
			handle,
			new(probe),
			2
		);
	}
}
