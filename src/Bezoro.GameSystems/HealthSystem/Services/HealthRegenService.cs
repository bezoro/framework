using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.GameSystems.HealthSystem.Abstractions;
using Bezoro.GameSystems.HealthSystem.Types;

namespace Bezoro.GameSystems.HealthSystem.Services;

/// <summary>
///     Provides timed health-over-time regeneration using async loops for smooth,
///     cup-filling restoration.
/// </summary>
public sealed class HealthRegenService : IHealthRegenService
{
	/// <summary>
	///     The interval between heal ticks for the per-second overload.
	///     50ms = 20 ticks per second for smooth restoration.
	/// </summary>
	private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(50);
	private readonly ConcurrentDictionary<IHealth, List<int>> _targetRegens = new();

	private readonly ConcurrentDictionary<int, RegenEntry> _regens = new();
	private          int                                   _nextId;

	/// <inheritdoc />
	public int ActiveCount => _regens.Count;

	/// <inheritdoc />
	public bool IsActive(RegenHandle handle) => handle.IsValid && _regens.ContainsKey(handle.Id);

	/// <inheritdoc />
	public bool Stop(RegenHandle handle)
	{
		if (!handle.IsValid)
			return false;

		if (!_regens.TryRemove(handle.Id, out var entry))
			return false;

		entry.Cts.Cancel();
		entry.Cts.Dispose();
		RemoveFromTargetTracking(entry.Target, handle.Id);
		return true;
	}

	/// <inheritdoc />
	public RegenHandle AddRegen(IHealth target, uint amountPerSecond, float durationSeconds) =>
		AddRegenInternal(target, amountPerSecond, durationSeconds);

	/// <inheritdoc />
	public RegenHandle AddRegen(IHealth target, uint totalAmount, uint ticks) =>
		AddRegenByTicks(target, totalAmount, ticks);

	/// <inheritdoc />
	public RegenHandle StartRegen(IHealth target, uint amountPerSecond, float durationSeconds)
	{
		StopAll(target);
		return AddRegenInternal(target, amountPerSecond, durationSeconds);
	}

	/// <inheritdoc />
	public RegenHandle StartRegen(IHealth target, uint totalAmount, uint ticks)
	{
		StopAll(target);
		return AddRegenByTicks(target, totalAmount, ticks);
	}

	/// <inheritdoc />
	public RegenHandle StartRepeatingRegen(IHealth target, uint amount, TimeSpan interval)
	{
		StopAll(target);
		return AddRepeatingRegenInternal(target, amount, interval);
	}

	/// <inheritdoc />
	public RegenHandle AddRepeatingRegen(IHealth target, uint amount, TimeSpan interval) =>
		AddRepeatingRegenInternal(target, amount, interval);

	/// <inheritdoc />
	public void StopAll(IHealth target)
	{
		if (!_targetRegens.TryRemove(target, out var regenIds))
			return;

		lock (regenIds)
		{
			foreach (int id in regenIds)
			{
				if (_regens.TryRemove(id, out var entry))
				{
					entry.Cts.Cancel();
					entry.Cts.Dispose();
				}
			}

			regenIds.Clear();
		}
	}

	private RegenHandle AddRegenByTicks(IHealth target, uint totalAmount, uint ticks)
	{
		if (ticks == 0u)
			throw new ArgumentOutOfRangeException(nameof(ticks), "Ticks must be positive.");

		if (totalAmount == 0u)
			throw new ArgumentOutOfRangeException(nameof(totalAmount), "Amount must be positive.");

		uint perTick   = totalAmount / ticks;
		uint remainder = totalAmount % ticks;

		return StartLoop(target, perTick, remainder, (int)ticks, TimeSpan.FromSeconds(1));
	}

	private RegenHandle AddRegenInternal(IHealth target, uint amountPerSecond, float durationSeconds)
	{
		if (durationSeconds <= 0f)
			throw new ArgumentOutOfRangeException(nameof(durationSeconds), "Duration must be positive.");

		if (amountPerSecond == 0u)
			throw new ArgumentOutOfRangeException(nameof(amountPerSecond), "Amount must be positive.");

		var totalAmount = (uint)(amountPerSecond * durationSeconds);
		int totalTicks  = Math.Max(1, (int)(durationSeconds / TickInterval.TotalSeconds));
		uint perTick    = totalAmount / (uint)totalTicks;
		uint remainder  = totalAmount % (uint)totalTicks;

		return StartLoop(target, perTick, remainder, totalTicks, TickInterval);
	}

	private RegenHandle StartLoop(IHealth target, uint perTick, uint remainder, int totalTicks, TimeSpan interval)
	{
		int regenId = Interlocked.Increment(ref _nextId);
		var handle  = new RegenHandle(regenId);
		var cts     = new CancellationTokenSource();

		var entry = new RegenEntry(target, cts);
		_regens.TryAdd(regenId, entry);
		TrackTarget(target, regenId);

		_ = RunLoopAsync(target, regenId, perTick, remainder, totalTicks, interval, cts.Token);

		return handle;
	}

	private async Task RunLoopAsync(
		IHealth           target,
		int               regenId,
		uint              perTick,
		uint              remainder,
		int               totalTicks,
		TimeSpan          interval,
		CancellationToken ct)
	{
		try
		{
			for (var i = 0; totalTicks < 0 || i < totalTicks; i++)
			{
				await Task.Delay(interval, ct).ConfigureAwait(false);

				uint healThisTick = perTick + ((uint)i < remainder ? 1u : 0u);
				target.RestoreCurrentHealthBy(healThisTick);
			}
		}
		catch (OperationCanceledException)
		{
			// Regen was stopped — expected
		}
		finally
		{
			if (_regens.TryRemove(regenId, out var entry))
			{
				RemoveFromTargetTracking(entry.Target, regenId);
				entry.Cts.Dispose();
			}
		}
	}

	private void RemoveFromTargetTracking(IHealth target, int regenId)
	{
		if (!_targetRegens.TryGetValue(target, out var list))
			return;

		lock (list)
		{
			list.Remove(regenId);
		}
	}

	private void TrackTarget(IHealth target, int regenId)
	{
		var list = _targetRegens.GetOrAdd(target, static _ => new());
		lock (list)
		{
			list.Add(regenId);
		}
	}

	private RegenHandle AddRepeatingRegenInternal(IHealth target, uint amount, TimeSpan interval)
	{
		if (amount == 0u)
			throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be positive.");

		if (interval <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(interval), "Interval must be positive.");

		return StartLoop(target, amount, 0u, -1, interval);
	}

	private readonly struct RegenEntry(IHealth target, CancellationTokenSource cts)
	{
		public readonly CancellationTokenSource Cts    = cts;
		public readonly IHealth                 Target = target;
	}
}
