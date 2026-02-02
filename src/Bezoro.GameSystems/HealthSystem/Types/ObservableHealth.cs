using System;
using Bezoro.Core.Types;
using Bezoro.Events.Abstractions;
using Bezoro.GameSystems.HealthSystem.Abstractions;

namespace Bezoro.GameSystems.HealthSystem.Types;

/// <summary>
///     Decorator that publishes <see cref="HealthChangedEvent" />s through an <see cref="IEventBus" />.
///     Events are enqueued after each delegated call.
/// </summary>
public sealed class ObservableHealth : IHealth, IExcessHealth
{
	private readonly IHealth        _inner;
	private readonly IExcessHealth? _excess;
	private readonly IEventBus      _bus;

	/// <summary>
	///     Creates an observable wrapper around the given health instance.
	/// </summary>
	/// <param name="inner">The health instance to wrap.</param>
	/// <param name="bus">The event bus used to enqueue change events.</param>
	public ObservableHealth(IHealth inner, IEventBus bus)
	{
		_inner  = inner ?? throw new ArgumentNullException(nameof(inner));
		_bus    = bus ?? throw new ArgumentNullException(nameof(bus));
		_excess = inner as IExcessHealth;
	}

	/// <summary>
	///     The wrapped health instance.
	/// </summary>
	public IHealth Inner => _inner;

	/// <summary>
	///     Gets whether the wrapped health supports excess health.
	/// </summary>
	public bool SupportsExcess => _excess is not null;

	public Percent Percentage => _inner.Percentage;
	public uint    Current    => _inner.Current;
	public uint    Max        => _inner.Max;
	public uint    Excess     => GetExcessHealth().Excess;

	public void DecreaseCurrentHealthBy(uint value)
	{
		var before = Capture();
		_inner.DecreaseCurrentHealthBy(value);
		var after = Capture();
		Enqueue(HealthChangeKind.DecreaseCurrent, value, MaxHealthUpdateMode.ClampCurrent, before, after);
	}

	public void DecreaseMaxHealthBy(uint value)
	{
		var before = Capture();
		_inner.DecreaseMaxHealthBy(value);
		var after = Capture();
		Enqueue(HealthChangeKind.DecreaseMax, value, MaxHealthUpdateMode.ClampCurrent, before, after);
	}

	public void DepleteCurrentHealth()
	{
		var before = Capture();
		_inner.DepleteCurrentHealth();
		var after = Capture();
		Enqueue(HealthChangeKind.DepleteCurrent, 0u, MaxHealthUpdateMode.ClampCurrent, before, after);
	}

	public void FullyRestoreCurrentHealth()
	{
		var before = Capture();
		_inner.FullyRestoreCurrentHealth();
		var after = Capture();
		Enqueue(HealthChangeKind.FullyRestoreCurrent, 0u, MaxHealthUpdateMode.ClampCurrent, before, after);
	}

	public void IncreaseCurrentHealthBy(uint value)
	{
		var before = Capture();
		_inner.IncreaseCurrentHealthBy(value);
		var after = Capture();
		Enqueue(HealthChangeKind.IncreaseCurrent, value, MaxHealthUpdateMode.ClampCurrent, before, after);
	}

	public void IncreaseMaxHealthBy(uint value)
	{
		var before = Capture();
		_inner.IncreaseMaxHealthBy(value);
		var after = Capture();
		Enqueue(HealthChangeKind.IncreaseMax, value, MaxHealthUpdateMode.ClampCurrent, before, after);
	}

	public void RestoreCurrentHealthBy(uint value)
	{
		var before = Capture();
		_inner.RestoreCurrentHealthBy(value);
		var after = Capture();
		Enqueue(HealthChangeKind.RestoreCurrent, value, MaxHealthUpdateMode.ClampCurrent, before, after);
	}

	public void SetCurrentHealthTo(uint value)
	{
		var before = Capture();
		_inner.SetCurrentHealthTo(value);
		var after = Capture();
		Enqueue(HealthChangeKind.SetCurrent, value, MaxHealthUpdateMode.ClampCurrent, before, after);
	}

	public void SetMaxHealthTo(uint value)
	{
		var before = Capture();
		_inner.SetMaxHealthTo(value);
		var after = Capture();
		Enqueue(HealthChangeKind.SetMax, value, MaxHealthUpdateMode.ClampCurrent, before, after);
	}

	public void DepleteExcessHealth()
	{
		var before = Capture();
		GetExcessHealth().DepleteExcessHealth();
		var after = Capture();
		Enqueue(HealthChangeKind.DepleteExcess, 0u, MaxHealthUpdateMode.ClampCurrent, before, after);
	}

	public void DecreaseExcessHealthBy(uint value)
	{
		var before = Capture();
		GetExcessHealth().DecreaseExcessHealthBy(value);
		var after = Capture();
		Enqueue(HealthChangeKind.DecreaseExcess, value, MaxHealthUpdateMode.ClampCurrent, before, after);
	}

	public void IncreaseExcessHealthBy(uint value)
	{
		var before = Capture();
		GetExcessHealth().IncreaseExcessHealthBy(value);
		var after = Capture();
		Enqueue(HealthChangeKind.IncreaseExcess, value, MaxHealthUpdateMode.ClampCurrent, before, after);
	}

	public void SetExcessHealthTo(uint value)
	{
		var before = Capture();
		GetExcessHealth().SetExcessHealthTo(value);
		var after = Capture();
		Enqueue(HealthChangeKind.SetExcess, value, MaxHealthUpdateMode.ClampCurrent, before, after);
	}

	private Snapshot Capture()
	{
		return new Snapshot(
			_inner.Current,
			_inner.Max,
			_excess?.Excess ?? 0u);
	}

	private void Enqueue(HealthChangeKind kind, uint value, MaxHealthUpdateMode mode, Snapshot before, Snapshot after)
	{
		_bus.Enqueue(new HealthChangedEvent(
			this,
			kind,
			value,
			mode,
			_excess is not null,
			before.Current,
			before.Max,
			before.Excess,
			after.Current,
			after.Max,
			after.Excess));
	}

	private IExcessHealth GetExcessHealth()
	{
		if (_excess is null)
			throw new NotSupportedException("Wrapped health does not implement IExcessHealth.");

		return _excess;
	}

	private readonly struct Snapshot
	{
		public readonly uint Current;
		public readonly uint Max;
		public readonly uint Excess;

		public Snapshot(uint current, uint max, uint excess)
		{
			Current = current;
			Max     = max;
			Excess  = excess;
		}
	}
}
