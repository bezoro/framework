using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Internal;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Services;

/// <summary>
///     Manages the registration and update cycle of systems within the Entity Component System (ECS) framework.
/// </summary>
internal sealed class SystemManager
{
	private readonly int               _maxDegreeOfParallelism;
	private readonly List<SystemState> _systems = [];

	/// <summary>
	///     Initializes a new instance of the <see cref="SystemManager" /> class with default parallelism.
	/// </summary>
	public SystemManager() : this(Environment.ProcessorCount) { }

	/// <summary>
	///     Initializes a new instance of the <see cref="SystemManager" /> class.
	/// </summary>
	/// <param name="maxDegreeOfParallelism">The maximum degree of parallelism for system updates.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxDegreeOfParallelism" /> is invalid.</exception>
	public SystemManager(int maxDegreeOfParallelism)
	{
		if (maxDegreeOfParallelism <= 0)
			throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), "Parallelism must be positive.");

		_maxDegreeOfParallelism = maxDegreeOfParallelism;
	}

	/// <summary>
	///     Registers a new system to be managed and updated.
	/// </summary>
	/// <param name="system">The system to register.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="system" /> is null.</exception>
	/// <exception cref="InvalidOperationException">Thrown when the system is already registered.</exception>
	public void RegisterSystem(ISystem system)
	{
		if (system is null) throw new ArgumentNullException(nameof(system));

		for (var i = 0; i < _systems.Count; i++)
		{
			if (ReferenceEquals(_systems[i].System, system))
				throw new InvalidOperationException("System is already registered.");
		}

		var readSet  = new HashSet<int>();
		var writeSet = new HashSet<int>();
		var accesses = system.Accesses ?? [];

		for (var i = 0; i < accesses.Length; i++)
		{
			var access = accesses[i];
			int typeId = ComponentTypeRegistry.GetOrCreate(access.ComponentType);

			if (access.Mode == ComponentAccessMode.ReadWrite)
			{
				writeSet.Add(typeId);
				readSet.Remove(typeId);
			}
			else if (!writeSet.Contains(typeId))
			{
				readSet.Add(typeId);
			}
		}

		_systems.Add(new(system, ToArray(readSet), ToArray(writeSet)));
	}

	/// <summary>
	///     Invokes the update logic of all registered systems.
	///     Typically called once per frame or simulation tick.
	/// </summary>
	/// <param name="world">The world context for systems to operate on.</param>
	/// <param name="deltaTime">The elapsed time since the last update.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="world" /> is null.</exception>
	public void UpdateAll(World world, float deltaTime)
	{
		if (world is null) throw new ArgumentNullException(nameof(world));

		if (_systems.Count == 0) return;

		var executions = new List<SystemExecution>(_systems.Count);
		for (var i = 0; i < _systems.Count; i++)
		{
			var state = _systems[i];
			if (!ShouldRun(state, deltaTime, out float effectiveDeltaTime)) continue;

			executions.Add(new(state, effectiveDeltaTime));
		}

		if (executions.Count == 0) return;

		var batches = BuildBatches(executions);
		var buffers = new List<CommandBuffer>();
		for (var i = 0; i < batches.Count; i++)
			ExecuteBatch(batches[i], world, buffers);

		for (var i = 0; i < buffers.Count; i++)
		{
			var buffer = buffers[i];
			if (buffer.HasCommands)
				buffer.Playback();
		}
	}

	private static bool ShouldRun(SystemState state, float deltaTime, out float effectiveDeltaTime)
	{
		const int maxCatchUpTicks = 3;

		var settings = state.System.UpdateSettings;
		if (settings.IntervalSeconds <= 0f)
		{
			effectiveDeltaTime = deltaTime;
			return true;
		}

		state.Accumulator += deltaTime;

		float maxAccumulator = settings.IntervalSeconds * maxCatchUpTicks;
		if (state.Accumulator > maxAccumulator)
			state.Accumulator = maxAccumulator;

		if (state.Accumulator < settings.IntervalSeconds)
		{
			effectiveDeltaTime = 0f;
			return false;
		}

		state.Accumulator  -= settings.IntervalSeconds;
		effectiveDeltaTime =  settings.IntervalSeconds;
		return true;
	}

	private static int[] ToArray(HashSet<int> set)
	{
		var array = new int[set.Count];
		var index = 0;
		foreach (int value in set)
			array[index++] = value;

		return array;
	}

	private static List<SystemBatch> BuildBatches(List<SystemExecution> executions)
	{
		var batches = new List<SystemBatch>();

		for (var i = 0; i < executions.Count; i++)
		{
			var execution = executions[i];
			var state     = execution.State;
			var added     = false;

			for (var b = 0; b < batches.Count; b++)
			{
				if (!batches[b].CanAdd(state)) continue;

				batches[b].Add(execution);
				added = true;
				break;
			}

			if (added) continue;

			var batch = new SystemBatch();
			batch.Add(execution);
			batches.Add(batch);
		}

		return batches;
	}

	private static void ExecuteSystem(SystemExecution execution, World world, CommandBuffer buffer)
	{
		var context = new SystemContext(execution.DeltaTime, buffer);
		execution.State.System.Update(world, in context);
	}

	private void ExecuteBatch(SystemBatch batch, World world, List<CommandBuffer> buffers)
	{
		if (batch.Systems.Count == 0) return;

		var batchBuffers = new CommandBuffer[batch.Systems.Count];

		if (batch.Systems.Count == 1 || _maxDegreeOfParallelism == 1)
		{
			var buffer = new CommandBuffer(world);
			batchBuffers[0] = buffer;
			ExecuteSystem(batch.Systems[0], world, buffer);
		}
		else
		{
			var options = new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism };
			Parallel.For(
				0, batch.Systems.Count, options, i =>
				{
					var buffer = new CommandBuffer(world);
					batchBuffers[i] = buffer;
					ExecuteSystem(batch.Systems[i], world, buffer);
				}
			);
		}

		for (var i = 0; i < batchBuffers.Length; i++)
			buffers.Add(batchBuffers[i]);
	}
}
