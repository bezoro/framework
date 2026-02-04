using Bezoro.Core.Types;
using Bezoro.GameSystems.HealthSystem.Abstractions;

namespace Bezoro.GameSystems.HealthSystem.Types;

/// <summary>
///     Default health implementation with excess health support.
/// </summary>
/// <remarks>
///     Immutable value type. Operations return updated instances.
/// </remarks>
public readonly record struct HealthWithExcess : IDamageableHealth<HealthWithExcess>
{
	private readonly UIntRange _base;
	private readonly UIntRange _excess;

	/// <summary>
	///     Initializes a new health instance with current set to max and no excess.
	/// </summary>
	/// <param name="max">The maximum base health.</param>
	public HealthWithExcess(uint max) : this(max, max, 0u) { }

	/// <summary>
	///     Initializes a new health instance with no excess.
	/// </summary>
	/// <param name="max">The maximum base health.</param>
	/// <param name="current">The current base health.</param>
	public HealthWithExcess(uint max, uint current) : this(max, current, 0u) { }

	/// <summary>
	///     Initializes a new health instance with excess capacity.
	/// </summary>
	/// <param name="max">The maximum base health.</param>
	/// <param name="current">The current health; overflow is routed to excess.</param>
	/// <param name="excess">Initial excess health.</param>
	/// <param name="excessMax">Maximum excess health.</param>
	public HealthWithExcess(uint max, uint current, uint excess, uint excessMax = 0u)
	{
		uint baseCurrent = current > max ? max : current;
		uint overflow    = current > baseCurrent ? current - baseCurrent : 0u;
		uint totalExcess = Saturate((ulong)excess + overflow);

		_base   = new(max, baseCurrent);
		_excess = new(excessMax, totalExcess);
	}

	private HealthWithExcess(UIntRange baseRange, UIntRange excessRange)
	{
		_base   = baseRange;
		_excess = excessRange;
	}

	/// <summary>
	///     Gets the base health as a percentage of base max.
	/// </summary>
	public Percent BasePercentage => _base.Percentage;

	/// <summary>
	///     Gets the excess health as a percentage of excess max.
	/// </summary>
	public Percent ExcessPercentage => _excess.Percentage;

	/// <summary>
	///     Gets the combined base + excess as a percentage of total max.
	/// </summary>
	public Percent TotalPercentage => Percent.FromTotals((_base.Current, _base.Max), (_excess.Current, _excess.Max));

	/// <summary>
	///     Gets the current base health value.
	/// </summary>
	public uint Current => _base.Current;

	/// <summary>
	///     Gets the effective current health used for damage calculations.
	/// </summary>
	public uint EffectiveCurrent => Saturate((ulong)Current + ExcessCurrent);

	/// <summary>
	///     Gets the current excess health value.
	/// </summary>
	public uint ExcessCurrent => _excess.Current;

	/// <summary>
	///     Gets the maximum excess health value.
	/// </summary>
	public uint ExcessMax => _excess.Max;

	/// <summary>
	///     Gets the maximum base health value.
	/// </summary>
	public uint Max => _base.Max;

	/// <summary>
	///     Applies damage and returns the updated health.
	/// </summary>
	/// <param name="amount">The amount of damage to apply.</param>
	/// <returns>The updated health.</returns>
	public HealthWithExcess ApplyDamage(uint amount) => DecreaseHealthBy(amount);

	/// <summary>
	///     Returns a new health with damage applied to base health only.
	/// </summary>
	/// <param name="value">The amount to subtract.</param>
	/// <returns>The updated health.</returns>
	public HealthWithExcess DecreaseCurrentHealthBy(uint value)
	{
		if (value == 0) return this;

		return new(_base.Decrease(value), _excess);
	}

	/// <summary>
	///     Returns a new health with excess health decreased by the specified amount.
	/// </summary>
	/// <param name="value">The amount to subtract from excess.</param>
	/// <returns>The updated health.</returns>
	public HealthWithExcess DecreaseExcessHealthBy(uint value)
	{
		if (value == 0) return this;

		return new(_base, _excess.Decrease(value));
	}

	/// <summary>
	///     Returns a new health with damage applied to excess first, then base health.
	/// </summary>
	/// <param name="value">The amount to subtract.</param>
	/// <returns>The updated health.</returns>
	public HealthWithExcess DecreaseHealthBy(uint value)
	{
		if (value == 0) return this;

		uint remaining     = value;
		uint excessCurrent = _excess.Current;
		var  baseRange     = _base;
		var  excessRange   = _excess;

		if (excessCurrent > 0)
		{
			uint absorbed = excessCurrent >= remaining ? remaining : excessCurrent;
			excessRange =  excessRange.Decrease(absorbed);
			remaining   -= absorbed;
		}

		if (remaining > 0)
			baseRange = baseRange.Decrease(remaining);

		return new(baseRange, excessRange);
	}

	/// <summary>
	///     Returns a new health with max decreased and current updated based on the chosen mode.
	/// </summary>
	/// <param name="value">The amount to subtract from max.</param>
	/// <param name="mode">How to update current relative to the new max.</param>
	/// <returns>The updated health.</returns>
	public HealthWithExcess DecreaseMaxHealthBy(uint value, MaxValueUpdateMode mode)
	{
		if (value == 0) return this;

		return new(
			_base.DecreaseMax(value, mode),
			_excess
		);
	}

	/// <summary>
	///     Returns a new health with base current set to zero.
	/// </summary>
	/// <returns>The updated health.</returns>
	public HealthWithExcess DepleteCurrentHealth() => new(_base.Deplete(), _excess);

	/// <summary>
	///     Returns a new health with excess current set to zero.
	/// </summary>
	/// <returns>The updated health.</returns>
	public HealthWithExcess DepleteExcessHealth() => new(_base, _excess.Deplete());

	/// <summary>
	///     Returns a new health with base current fully restored to max.
	/// </summary>
	/// <returns>The updated health.</returns>
	public HealthWithExcess FullyRestoreCurrentHealth() => new(_base.FullyRestore(), _excess);

	/// <summary>
	///     Returns a new health with base restored and any overflow routed into excess.
	/// </summary>
	/// <param name="value">The amount to restore.</param>
	/// <returns>The updated health.</returns>
	public HealthWithExcess IncreaseCurrentHealthBy(uint value)
	{
		if (value == 0) return this;

		var   baseRange   = _base;
		var   excessRange = _excess;
		ulong sum         = (ulong)baseRange.Current + value;
		if (sum <= baseRange.Max)
			return new(baseRange.Restore(value), excessRange);

		if (baseRange.Current < baseRange.Max)
			baseRange = baseRange.SetCurrent(baseRange.Max);

		ulong overflow = sum - baseRange.Max;
		uint  add      = Saturate(overflow);
		if (add > 0)
			excessRange = excessRange.Restore(add);

		return new(baseRange, excessRange);
	}

	/// <summary>
	///     Returns a new health with excess restored by the specified amount.
	/// </summary>
	/// <param name="value">The amount to restore in excess.</param>
	/// <returns>The updated health.</returns>
	public HealthWithExcess IncreaseExcessHealthBy(uint value)
	{
		if (value == 0) return this;

		return new(_base, _excess.Restore(value));
	}

	/// <summary>
	///     Returns a new health with max increased and current updated based on the chosen mode.
	/// </summary>
	/// <param name="value">The amount to add to max.</param>
	/// <param name="mode">How to update current relative to the new max.</param>
	/// <returns>The updated health.</returns>
	public HealthWithExcess IncreaseMaxHealthBy(uint value, MaxValueUpdateMode mode)
	{
		if (value == 0) return this;

		return new(
			_base.IncreaseMax(value, mode),
			_excess
		);
	}

	/// <summary>
	///     Returns a new health with base current restored, capped at max, without creating excess.
	/// </summary>
	/// <param name="value">The amount to restore.</param>
	/// <returns>The updated health.</returns>
	public HealthWithExcess RestoreCurrentHealthBy(uint value)
	{
		if (value == 0) return this;

		return new(_base.Restore(value), _excess);
	}

	/// <summary>
	///     Returns a new health with base current set to the specified value, clamped to max.
	/// </summary>
	/// <param name="value">The new base current value.</param>
	/// <returns>The updated health.</returns>
	public HealthWithExcess SetCurrentHealthTo(uint value) => new(_base.SetCurrent(value), _excess);

	/// <summary>
	///     Returns a new health with excess current set to the specified value, clamped to excess max.
	/// </summary>
	/// <param name="value">The new excess current value.</param>
	/// <returns>The updated health.</returns>
	public HealthWithExcess SetExcessHealthTo(uint value) => new(_base, _excess.SetCurrent(value));

	/// <summary>
	///     Returns a new health with max set and current updated based on the chosen mode.
	/// </summary>
	/// <param name="value">The new maximum health.</param>
	/// <param name="mode">How to update current relative to the new max.</param>
	/// <returns>The updated health.</returns>
	public HealthWithExcess SetMaxHealthTo(uint value, MaxValueUpdateMode mode) =>
		new(_base.SetMax(value, mode), _excess);

	private static uint Saturate(ulong value) => value >= uint.MaxValue ? uint.MaxValue : (uint)value;
}
