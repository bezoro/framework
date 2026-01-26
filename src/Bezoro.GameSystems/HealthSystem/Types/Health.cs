using System;
using Bezoro.Core.Types;
using Bezoro.GameSystems.HealthSystem.Abstractions;

namespace Bezoro.GameSystems.HealthSystem.Types;

/// <summary>
///     Default health implementation with optional excess health support.
/// </summary>
public sealed class Health : IHealth, IExcessHealth
{
	public Health(uint max) : this(max, max) { }

	public Health(uint max, uint current, uint excess = 0u)
	{
		Max = max;

		uint clampedCurrent = current > Max ? Max : current;
		uint overflow       = current > clampedCurrent ? current - clampedCurrent : 0u;

		Current = clampedCurrent;
		Excess  = Saturate((ulong)excess + overflow);
	}

	public Percent Percentage => new(Current, Max);

	public uint Current { get; private set; }
	public uint Excess  { get; private set; }
	public uint Max     { get; private set; }

	public void ClearExcessHealth() => Excess = 0;

	public void DecreaseCurrentHealthBy(uint value)
	{
		if (value == 0) return;

		uint remaining = value;
		if (Excess > 0)
		{
			uint absorbed = Excess >= remaining ? remaining : Excess;
			Excess    -= absorbed;
			remaining -= absorbed;
		}

		if (remaining > 0)
			Current = remaining >= Current ? 0u : Current - remaining;
	}

	public void DecreaseExcessHealthBy(uint value)
	{
		if (value == 0) return;

		Excess = value >= Excess ? 0u : Excess - value;
	}

	public void DecreaseMaxHealthBy(uint value)
	{
		if (value == 0) return;

		uint newMax = value >= Max ? 0u : Max - value;
		SetMaxHealthTo(newMax, MaxHealthUpdateMode.ClampCurrent);
	}

	public void DepleteCurrentHealth()      => Current = 0;
	public void FullyRestoreCurrentHealth() => Current = Max;

	public void IncreaseCurrentHealthBy(uint value)
	{
		if (value == 0) return;

		ulong sum = (ulong)Current + value;
		if (sum <= Max)
		{
			Current = (uint)sum;
			return;
		}

		Current = Max;
		AddExcessInternal(sum - Max);
	}

	public void IncreaseExcessHealthBy(uint value)
	{
		if (value == 0) return;

		AddExcessInternal(value);
	}

	public void IncreaseMaxHealthBy(uint value)
	{
		if (value == 0) return;

		uint newMax = Saturate((ulong)Max + value);
		SetMaxHealthTo(newMax, MaxHealthUpdateMode.ClampCurrent);
	}

	public void RestoreCurrentHealthBy(uint value)
	{
		if (value == 0) return;

		ulong newCurrent = (ulong)Current + value;
		Current = newCurrent >= Max ? Max : (uint)newCurrent;
	}

	public void SetCurrentHealthTo(uint value) => Current = value > Max ? Max : value;
	public void SetExcessHealthTo(uint  value) => Excess = value;
	public void SetMaxHealthTo(uint     value) => SetMaxHealthTo(value, MaxHealthUpdateMode.ClampCurrent);

	public void SetMaxHealthTo(uint value, MaxHealthUpdateMode mode)
	{
		uint oldMax = Max;
		uint newMax = value;

		switch (mode)
		{
			case MaxHealthUpdateMode.PreservePercentage when oldMax > 0:
			{
				float percent = (float)Current / oldMax;
				var   scaled  = (uint)MathF.Round(percent * newMax, MidpointRounding.AwayFromZero);
				Current = scaled > newMax ? newMax : scaled;
				break;
			}
			case MaxHealthUpdateMode.ClampCurrent:
			default:
				Current = Current > newMax ? newMax : Current;
				break;
		}

		Max = newMax;
	}

	private static uint Saturate(ulong value) => value >= uint.MaxValue ? uint.MaxValue : (uint)value;

	private void AddExcessInternal(ulong value)
	{
		if (value == 0) return;

		Excess = Saturate(Excess + value);
	}
}
