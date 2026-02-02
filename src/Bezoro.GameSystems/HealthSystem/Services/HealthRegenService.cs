using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Bezoro.GameSystems.HealthSystem.Abstractions;
using Bezoro.GameSystems.HealthSystem.Types;

namespace Bezoro.GameSystems.HealthSystem.Services;

/// <summary>
///     Provides timed health-over-time regeneration using a single timer and batch processing.
///     Zero per-tick heap allocations. Thread-safe.
/// </summary>
public sealed class HealthRegenService : IHealthRegenService
{
	private readonly object                            _lock         = new();
	private readonly Dictionary<int, int>              _idToIndex    = new();
	private readonly Dictionary<IHealth, HashSet<int>> _targetRegens = new();

	private RegenEntry[] _entries = new RegenEntry[8];
	private int          _count;
	private int          _nextId;

	private readonly Timer? _timer;
	private readonly bool   _isManual;
	private long            _lastTimestamp;
	private int             _ticking; // re-entrancy guard via Interlocked

	/// <summary>
	///     Creates a service with an internal timer that auto-ticks on a thread pool thread.
	/// </summary>
	/// <param name="timerResolutionMs">Timer period in milliseconds (default 5).</param>
	public HealthRegenService(uint timerResolutionMs = 5)
	{
		_lastTimestamp = Stopwatch.GetTimestamp();
		_timer = new Timer(OnTimerCallback, null, timerResolutionMs, timerResolutionMs);
	}

	/// <summary>
	///     Private constructor for manual-tick mode (no timer).
	/// </summary>
	private HealthRegenService()
	{
		_timer = null;
		_isManual = true;
	}

	/// <summary>
	///     Creates a service without an internal timer. Caller drives timing via <see cref="Update" />.
	///     Intended for deterministic testing or caller-driven update loops.
	/// </summary>
	public static HealthRegenService CreateManual() => new HealthRegenService();

	/// <inheritdoc />
	public int ActiveCount
	{
		get
		{
			lock (_lock)
				return _count;
		}
	}

	/// <inheritdoc />
	public bool IsActive(RegenHandle handle)
	{
		if (!handle.IsValid) return false;

		lock (_lock)
			return _idToIndex.ContainsKey(handle.Id);
	}

	/// <inheritdoc />
	public RegenHandle StartRegen(IHealth target, float amountPerSec, TimeSpan duration, uint tickFrequencyMs = 20)
	{
		ValidateCommon(target, amountPerSec, tickFrequencyMs);
		ValidateDuration(duration);

		lock (_lock)
		{
			StopAllInternal(target);
			return AddFiniteInternal(target, amountPerSec, duration, tickFrequencyMs);
		}
	}

	/// <inheritdoc />
	public RegenHandle AddRegen(IHealth target, float amountPerSec, TimeSpan duration, uint tickFrequencyMs = 20)
	{
		ValidateCommon(target, amountPerSec, tickFrequencyMs);
		ValidateDuration(duration);

		lock (_lock)
			return AddFiniteInternal(target, amountPerSec, duration, tickFrequencyMs);
	}

	/// <inheritdoc />
	public RegenHandle StartRepeatingRegen(IHealth target, float amountPerSec, uint tickFrequencyMs = 20)
	{
		ValidateCommon(target, amountPerSec, tickFrequencyMs);

		lock (_lock)
		{
			StopAllInternal(target);
			return AddInfiniteInternal(target, amountPerSec, tickFrequencyMs);
		}
	}

	/// <inheritdoc />
	public RegenHandle AddRepeatingRegen(IHealth target, float amountPerSec, uint tickFrequencyMs = 20)
	{
		ValidateCommon(target, amountPerSec, tickFrequencyMs);

		lock (_lock)
			return AddInfiniteInternal(target, amountPerSec, tickFrequencyMs);
	}

	/// <inheritdoc />
	public bool Stop(RegenHandle handle)
	{
		if (!handle.IsValid) return false;

		lock (_lock)
			return RemoveEntry(handle.Id);
	}

	/// <inheritdoc />
	public void StopAll(IHealth target)
	{
		lock (_lock)
			StopAllInternal(target);
	}

	/// <inheritdoc />
	public void Update(float deltaTime)
	{
		if (!_isManual)
			throw new InvalidOperationException("Update must not be called on a timer-driven service. Use CreateManual() for caller-driven updates.");

		if (deltaTime <= 0f) return;

		UpdateCore(deltaTime);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_timer is null) return;

		using var done = new ManualResetEvent(false);
		_timer.Dispose(done);
		done.WaitOne();
	}

	private void OnTimerCallback(object? state)
	{
		if (Interlocked.CompareExchange(ref _ticking, 1, 0) != 0)
			return;

		try
		{
			long now  = Stopwatch.GetTimestamp();
			long last = Interlocked.Exchange(ref _lastTimestamp, now);
			float dt  = (float)(now - last) / Stopwatch.Frequency;

			if (dt > 0f)
				UpdateCore(dt);
		}
		finally
		{
			Interlocked.Exchange(ref _ticking, 0);
		}
	}

	private void UpdateCore(float dt)
	{
		lock (_lock)
		{
			for (int i = _count - 1; i >= 0; i--)
			{
				ref var entry = ref _entries[i];
				entry.TimeAccumulator += dt;

				if (entry.IsFinite)
					ProcessFiniteEntry(ref entry, i);
				else
					ProcessInfiniteEntry(ref entry);
			}
		}
	}

	private void ProcessFiniteEntry(ref RegenEntry entry, int index)
	{
		bool completed = false;

		while (entry.TimeAccumulator >= entry.TickIntervalSec && entry.CurrentTick < entry.TotalTicks)
		{
			entry.TimeAccumulator -= entry.TickIntervalSec;
			entry.CurrentTick++;

			uint expectedSoFar = (uint)((ulong)entry.TotalAmount * (uint)entry.CurrentTick / (uint)entry.TotalTicks);
			uint healThisTick  = expectedSoFar - entry.TotalDelivered;

			if (healThisTick > 0u)
				entry.Target.RestoreCurrentHealthBy(healThisTick);

			entry.TotalDelivered = expectedSoFar;

			if (entry.CurrentTick >= entry.TotalTicks)
			{
				completed = true;
				break;
			}
		}

		if (completed)
		{
			// Capture before swapback invalidates the ref
			int    id     = entry.Id;
			var    target = entry.Target;
			SwapbackRemoveAt(index);
			UntrackRegen(target, id);
		}
	}

	private static void ProcessInfiniteEntry(ref RegenEntry entry)
	{
		while (entry.TimeAccumulator >= entry.TickIntervalSec)
		{
			entry.TimeAccumulator -= entry.TickIntervalSec;
			entry.Accumulator += entry.HealPerTick;

			var toHeal = (uint)entry.Accumulator;
			if (toHeal > 0u)
			{
				entry.Target.RestoreCurrentHealthBy(toHeal);
				entry.Accumulator -= toHeal;
			}
		}
	}

	private RegenHandle AddFiniteInternal(IHealth target, float amountPerSec, TimeSpan duration, uint tickFrequencyMs)
	{
		double rawTotal  = Math.Round((double)amountPerSec * duration.TotalSeconds, MidpointRounding.AwayFromZero);
		uint totalAmount = rawTotal >= uint.MaxValue ? uint.MaxValue : (uint)rawTotal;
		int totalTicks   = Math.Max(1, (int)(duration.TotalMilliseconds / tickFrequencyMs));

		int id = ++_nextId;
		var entry = new RegenEntry
		{
			Id              = id,
			Target          = target,
			IsFinite        = true,
			TickIntervalSec = tickFrequencyMs / 1000f,
			TotalAmount     = totalAmount,
			TotalTicks      = totalTicks,
		};

		AddEntry(id, target, entry);
		return new RegenHandle(id);
	}

	private RegenHandle AddInfiniteInternal(IHealth target, float amountPerSec, uint tickFrequencyMs)
	{
		double healPerTick = (double)amountPerSec * tickFrequencyMs / 1000.0;

		int id = ++_nextId;
		var entry = new RegenEntry
		{
			Id              = id,
			Target          = target,
			IsFinite        = false,
			TickIntervalSec = tickFrequencyMs / 1000f,
			HealPerTick     = healPerTick,
		};

		AddEntry(id, target, entry);
		return new RegenHandle(id);
	}

	private void AddEntry(int id, IHealth target, RegenEntry entry)
	{
		if (_count == _entries.Length)
			Array.Resize(ref _entries, _entries.Length * 2);

		_entries[_count] = entry;
		_idToIndex[id] = _count;
		_count++;

		if (!_targetRegens.TryGetValue(target, out var ids))
		{
			ids = new HashSet<int>();
			_targetRegens[target] = ids;
		}

		ids.Add(id);
	}

	/// <summary>
	///     Removes an entry by id. Handles both array removal and target tracking cleanup.
	/// </summary>
	private bool RemoveEntry(int id)
	{
		if (!_idToIndex.TryGetValue(id, out int index))
			return false;

		var target = _entries[index].Target;
		SwapbackRemoveAt(index);
		UntrackRegen(target, id);
		return true;
	}

	/// <summary>
	///     Low-level array removal via swapback. Only manages <see cref="_entries" />,
	///     <see cref="_count" />, and <see cref="_idToIndex" />. Caller must handle
	///     <see cref="_targetRegens" /> cleanup.
	/// </summary>
	private void SwapbackRemoveAt(int index)
	{
		int id = _entries[index].Id;
		_idToIndex.Remove(id);

		int lastIndex = _count - 1;
		if (index < lastIndex)
		{
			_entries[index] = _entries[lastIndex];
			_idToIndex[_entries[index].Id] = index;
		}

		_entries[lastIndex] = default;
		_count--;
	}

	/// <summary>
	///     Removes a single regen id from target tracking. Cleans up the target key if empty.
	/// </summary>
	private void UntrackRegen(IHealth target, int id)
	{
		if (!_targetRegens.TryGetValue(target, out var ids))
			return;

		ids.Remove(id);
		if (ids.Count == 0)
			_targetRegens.Remove(target);
	}

	private void StopAllInternal(IHealth target)
	{
		if (!_targetRegens.TryGetValue(target, out var ids))
			return;

		int count = ids.Count;
		if (count == 0)
		{
			_targetRegens.Remove(target);
			return;
		}

		// Copy ids to avoid modifying the set during iteration
		Span<int> idsCopy = count <= 16 ? stackalloc int[count] : new int[count];
		int i = 0;
		foreach (int id in ids)
			idsCopy[i++] = id;

		foreach (int id in idsCopy)
		{
			if (_idToIndex.TryGetValue(id, out int index))
				SwapbackRemoveAt(index);
		}

		_targetRegens.Remove(target);
	}

	private static void ValidateCommon(IHealth target, float amountPerSec, uint tickFrequencyMs)
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));

		if (amountPerSec <= 0f || float.IsNaN(amountPerSec) || float.IsInfinity(amountPerSec))
			throw new ArgumentOutOfRangeException(nameof(amountPerSec), "Amount per second must be a positive finite number.");

		if (tickFrequencyMs == 0u)
			throw new ArgumentOutOfRangeException(nameof(tickFrequencyMs), "Tick frequency must be positive.");
	}

	private static void ValidateDuration(TimeSpan duration)
	{
		if (duration <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be positive. Use StartRepeatingRegen/AddRepeatingRegen for infinite regen.");
	}

	/// <summary>
	///     Internal entry for a single regen effect. Finite and infinite regens share the same struct
	///     layout (~64 bytes) to avoid polymorphic dispatch and enable contiguous array storage.
	///     Unused fields for each mode default to zero and are never read.
	/// </summary>
	private struct RegenEntry
	{
		public int     Id;
		public IHealth Target;
		public bool    IsFinite;
		public float   TickIntervalSec;
		public float   TimeAccumulator;
		// Finite-only
		public uint TotalAmount;
		public int  TotalTicks;
		public int  CurrentTick;
		public uint TotalDelivered;
		// Infinite-only
		public double HealPerTick;
		public double Accumulator;
	}
}
