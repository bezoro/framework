using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.GameSystems.HealthSystem.Abstractions;
using Bezoro.GameSystems.HealthSystem.Types;

namespace Bezoro.GameSystems.HealthSystem.Services;

/// <summary>
///     Provides timed health-over-time regeneration using async loops.
///     Finite regens use integer distribution for exact HP delivery.
///     Infinite regens use a double accumulator for precision.
/// </summary>
public sealed class HealthRegenService : IHealthRegenService
{
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
	public RegenHandle StartRegen(IHealth target, float amountPerSec, TimeSpan duration, uint tickFrequencyMs = 20)
	{
		StopAll(target);
		return AddFiniteCore(target, amountPerSec, duration, tickFrequencyMs);
	}

	/// <inheritdoc />
	public RegenHandle AddRegen(IHealth target, float amountPerSec, TimeSpan duration, uint tickFrequencyMs = 20) =>
		AddFiniteCore(target, amountPerSec, duration, tickFrequencyMs);

	/// <inheritdoc />
	public RegenHandle StartRepeatingRegen(IHealth target, float amountPerSec, uint tickFrequencyMs = 20)
	{
		StopAll(target);
		return AddInfiniteCore(target, amountPerSec, tickFrequencyMs);
	}

	/// <inheritdoc />
	public RegenHandle AddRepeatingRegen(IHealth target, float amountPerSec, uint tickFrequencyMs = 20) =>
		AddInfiniteCore(target, amountPerSec, tickFrequencyMs);

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

	private static void ValidateCommon(IHealth target, float amountPerSec, uint tickFrequencyMs)
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));

		if (amountPerSec <= 0f || float.IsNaN(amountPerSec) || float.IsInfinity(amountPerSec))
			throw new ArgumentOutOfRangeException(nameof(amountPerSec), "Amount per second must be a positive finite number.");

		if (tickFrequencyMs == 0u)
			throw new ArgumentOutOfRangeException(nameof(tickFrequencyMs), "Tick frequency must be positive.");
	}

	private RegenHandle AddFiniteCore(IHealth target, float amountPerSec, TimeSpan duration, uint tickFrequencyMs)
	{
		ValidateCommon(target, amountPerSec, tickFrequencyMs);

		if (duration <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be positive. Use StartRepeatingRegen/AddRepeatingRegen for infinite regen.");

		double rawTotal = Math.Round((double)amountPerSec * duration.TotalSeconds, MidpointRounding.AwayFromZero);
		uint totalAmount = rawTotal >= uint.MaxValue ? uint.MaxValue : (uint)rawTotal;
		int totalTicks = Math.Max(1, (int)(duration.TotalMilliseconds / tickFrequencyMs));
		var interval = TimeSpan.FromMilliseconds(tickFrequencyMs);

		return StartLoop(target, (t, id, ct) => RunFiniteLoopAsync(t, id, totalAmount, totalTicks, interval, ct));
	}

	private RegenHandle AddInfiniteCore(IHealth target, float amountPerSec, uint tickFrequencyMs)
	{
		ValidateCommon(target, amountPerSec, tickFrequencyMs);

		double healPerTick = (double)amountPerSec * tickFrequencyMs / 1000.0;
		var interval = TimeSpan.FromMilliseconds(tickFrequencyMs);

		return StartLoop(target, (t, id, ct) => RunInfiniteLoopAsync(t, id, healPerTick, interval, ct));
	}

	private RegenHandle StartLoop(IHealth target, Func<IHealth, int, CancellationToken, Task> loopFactory)
	{
		int regenId = Interlocked.Increment(ref _nextId);
		var handle  = new RegenHandle(regenId);
		var cts     = new CancellationTokenSource();

		var entry = new RegenEntry(target, cts);
		_regens.TryAdd(regenId, entry);
		TrackTarget(target, regenId);

		_ = loopFactory(target, regenId, cts.Token);

		return handle;
	}

	private async Task RunFiniteLoopAsync(
		IHealth           target,
		int               regenId,
		uint              totalAmount,
		int               totalTicks,
		TimeSpan          interval,
		CancellationToken ct)
	{
		uint totalDelivered = 0;

		try
		{
			for (var i = 1; i <= totalTicks; i++)
			{
				await Task.Delay(interval, ct).ConfigureAwait(false);

				uint expectedSoFar = (uint)((ulong)totalAmount * (uint)i / (uint)totalTicks);
				uint healThisTick = expectedSoFar - totalDelivered;

				if (healThisTick > 0u)
					target.RestoreCurrentHealthBy(healThisTick);

				totalDelivered = expectedSoFar;
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

	private async Task RunInfiniteLoopAsync(
		IHealth           target,
		int               regenId,
		double            healPerTick,
		TimeSpan          interval,
		CancellationToken ct)
	{
		double accumulator = 0.0;

		try
		{
			while (true)
			{
				await Task.Delay(interval, ct).ConfigureAwait(false);

				accumulator += healPerTick;
				var toHeal = (uint)accumulator;

				if (toHeal > 0u)
				{
					target.RestoreCurrentHealthBy(toHeal);
					accumulator -= toHeal;
				}
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

	private readonly struct RegenEntry(IHealth target, CancellationTokenSource cts)
	{
		public readonly CancellationTokenSource Cts    = cts;
		public readonly IHealth                 Target = target;
	}
}
