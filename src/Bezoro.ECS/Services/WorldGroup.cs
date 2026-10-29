using System.Runtime.CompilerServices;
using Bezoro.ECS.Internal;
using Bezoro.ECS.Types;

namespace Bezoro.ECS.Services;

/// <summary>
///     Steps a fixed set of independent worlds using a bounded number of workers.
/// </summary>
/// <remarks>
///     Membership is copied at construction. The group does not own or dispose its worlds.
///     Each world must have exclusive access to its mutable systems and resources during a step.
///     Engine objects requiring the Unity or Godot main thread must not be accessed by worker systems.
///     Configure each world's own parallelism as one when parallelizing primarily across worlds.
/// </remarks>
public sealed class WorldGroup
{
	private readonly World[] _worlds;
	private readonly Action<int> _stepWorld;
	private int _running;
	private SystemLoopPhase _phase;
	private float _deltaTime;
	private CancellationToken _cancellationToken;

	/// <summary>
	///     Copies the supplied worlds and configures the maximum number stepped concurrently.
	/// </summary>
	/// <param name="worlds">Independent worlds, with no null or duplicate references.</param>
	/// <param name="maxDegreeOfParallelism">Worker limit. One selects sequential execution.</param>
	/// <exception cref="ArgumentNullException">The world collection is null.</exception>
	/// <exception cref="ArgumentException">A world is null or appears more than once.</exception>
	/// <exception cref="ArgumentOutOfRangeException">The worker limit is not positive.</exception>
	/// <exception cref="ObjectDisposedException">A world has already been disposed.</exception>
	public WorldGroup(IEnumerable<World> worlds, int maxDegreeOfParallelism = 1)
	{
		if (worlds is null) throw new ArgumentNullException(nameof(worlds));
		if (maxDegreeOfParallelism <= 0)
			throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));

		var seen = new HashSet<World>(WorldIdentityComparer.Instance);
		var copied = new List<World>();
		foreach (var world in worlds)
		{
			if (world is null)
				throw new ArgumentException("World groups cannot contain null worlds.", nameof(worlds));
			if (!seen.Add(world))
				throw new ArgumentException("World groups cannot contain the same world twice.", nameof(worlds));
			_ = world.EntityCount;
			copied.Add(world);
		}

		_worlds = copied.ToArray();
		Worlds = Array.AsReadOnly(_worlds);
		MaxDegreeOfParallelism = maxDegreeOfParallelism;
		_stepWorld = StepWorld;
	}

	/// <summary>Gets the immutable membership list, in construction order.</summary>
	public IReadOnlyList<World> Worlds { get; }

	/// <summary>Gets the maximum number of worlds stepped concurrently.</summary>
	public int MaxDegreeOfParallelism { get; }

	/// <summary>Steps the regular tick phase for every world.</summary>
	/// <param name="deltaTime">Finite, non-negative elapsed seconds.</param>
	/// <param name="cancellationToken">Stops dispatching worlds that have not started.</param>
	public void Tick(float deltaTime, CancellationToken cancellationToken = default) =>
		RunPhase(SystemLoopPhase.Tick, deltaTime, cancellationToken);

	/// <summary>Steps the fixed tick phase for every world.</summary>
	/// <param name="deltaTime">Finite, non-negative elapsed seconds.</param>
	/// <param name="cancellationToken">Stops dispatching worlds that have not started.</param>
	public void FixedTick(float deltaTime, CancellationToken cancellationToken = default) =>
		RunPhase(SystemLoopPhase.FixedTick, deltaTime, cancellationToken);

	/// <summary>Steps the late tick phase for every world.</summary>
	/// <param name="deltaTime">Finite, non-negative elapsed seconds.</param>
	/// <param name="cancellationToken">Stops dispatching worlds that have not started.</param>
	public void LateTick(float deltaTime, CancellationToken cancellationToken = default) =>
		RunPhase(SystemLoopPhase.LateTick, deltaTime, cancellationToken);

	/// <summary>
	///     Steps a phase and joins all started workers before returning or propagating a failure.
	/// </summary>
	/// <param name="loopPhase">Host loop phase to execute.</param>
	/// <param name="deltaTime">Finite, non-negative elapsed seconds.</param>
	/// <param name="cancellationToken">Stops dispatching worlds that have not started.</param>
	/// <remarks>
	///     Cancellation and failures do not roll back completed work. In-flight world updates finish before this
	///     method exits. A failed or canceled step may leave some worlds unstepped. Membership order determines
	///     sequential execution order; worker execution order is unspecified when the worker limit exceeds one.
	///     Calls on the same group must not overlap. External access or disposal of its worlds must wait for this call.
	/// </remarks>
	/// <exception cref="InvalidOperationException">An update on this group is already running.</exception>
	/// <exception cref="ArgumentOutOfRangeException">The phase is unknown or elapsed time is invalid.</exception>
	/// <exception cref="OperationCanceledException">Cancellation was requested.</exception>
	/// <exception cref="ObjectDisposedException">A world has already been disposed.</exception>
	public void RunPhase(SystemLoopPhase loopPhase, float deltaTime, CancellationToken cancellationToken = default)
	{
		if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0f)
			throw new ArgumentOutOfRangeException(nameof(deltaTime), "Elapsed time must be finite and non-negative.");
		if (loopPhase is not (SystemLoopPhase.Tick or SystemLoopPhase.FixedTick or SystemLoopPhase.LateTick))
			throw new ArgumentOutOfRangeException(nameof(loopPhase));
		cancellationToken.ThrowIfCancellationRequested();
		if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
			throw new InvalidOperationException("A world group update is already running.");

		try
		{
			// Validate every lifetime before any world begins mutating. External disposal
			// during a step remains a caller ownership violation.
			foreach (var world in _worlds)
				_ = world.EntityCount;
			_phase = loopPhase;
			_deltaTime = deltaTime;
			_cancellationToken = cancellationToken;
			ParallelWorkScheduler.Execute(_worlds.Length, MaxDegreeOfParallelism, _stepWorld);
			cancellationToken.ThrowIfCancellationRequested();
		}
		finally
		{
			_cancellationToken = default;
			Volatile.Write(ref _running, 0);
		}
	}

	private void StepWorld(int index)
	{
		_cancellationToken.ThrowIfCancellationRequested();
		_worlds[index].RunPhase(_phase, _deltaTime);
	}

	private sealed class WorldIdentityComparer : IEqualityComparer<World>
	{
		public static WorldIdentityComparer Instance { get; } = new();
		public bool Equals(World? x, World? y) => ReferenceEquals(x, y);
		public int GetHashCode(World obj) => RuntimeHelpers.GetHashCode(obj);
	}
}
