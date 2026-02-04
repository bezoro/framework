namespace Bezoro.Core.Types;

/// <summary>
///     Represents a bounded unsigned integer range with current and maximum values.
/// </summary>
/// <remarks>
///     Immutable value type. Use the instance methods to create updated ranges.
/// </remarks>
public readonly record struct UIntRange
{
	/// <summary>
	///     Initializes a new range with the specified max and current value.
	/// </summary>
	/// <param name="max">The maximum value.</param>
	/// <param name="current">The current value.</param>
	public UIntRange(uint max, uint current)
	{
		Max     = max;
		Current = current > max ? max : current;
	}

	/// <summary>
	///     Gets the minimum value for the range (always zero).
	/// </summary>
	public static uint Min => 0u;

	/// <summary>
	///     Gets the current value as a percentage of the maximum.
	/// </summary>
	public Percent Percentage => new(Current, Max);

	/// <summary>
	///     Gets the current value.
	/// </summary>
	public uint Current { get; }

	/// <summary>
	///     Gets the maximum value.
	/// </summary>
	public uint Max { get; }

	/// <summary>
	///     Returns a new range with the current value decreased by the specified amount, clamped to zero.
	/// </summary>
	public UIntRange Decrease(uint value)
	{
		if (value == 0) return this;

		uint newCurrent = value >= Current ? 0u : Current - value;
		return new(Max, newCurrent);
	}

	/// <summary>
	///     Returns a new range with the maximum value decreased, clamped to zero, and current updated accordingly.
	/// </summary>
	/// <param name="value">The amount to subtract from the maximum value.</param>
	/// <param name="preservePercentage">
	///     Whether to preserve the current percentage relative to the previous max.
	/// </param>
	public UIntRange DecreaseMax(uint value, bool preservePercentage)
	{
		if (value == 0) return this;

		uint newMax = value >= Max ? 0u : Max - value;
		return SetMax(newMax, preservePercentage);
	}

	/// <summary>
	///     Returns a new range with the current value set to zero.
	/// </summary>
	public UIntRange Deplete() => new(Max, 0u);

	/// <summary>
	///     Returns a new range with the current value set to max.
	/// </summary>
	public UIntRange FullyRestore() => new(Max, Max);

	/// <summary>
	///     Returns a new range with the maximum value increased, saturated at <see cref="uint.MaxValue" />, and current
	///     updated accordingly.
	/// </summary>
	/// <param name="value">The amount to add to the maximum value.</param>
	/// <param name="preservePercentage">
	///     Whether to preserve the current percentage relative to the previous max.
	/// </param>
	public UIntRange IncreaseMax(uint value, bool preservePercentage)
	{
		if (value == 0) return this;

		uint newMax = Saturate((ulong)Max + value);
		return SetMax(newMax, preservePercentage);
	}

	/// <summary>
	///     Returns a new range with the current value restored by the specified amount, capped at max.
	/// </summary>
	public UIntRange Restore(uint value)
	{
		if (value == 0) return this;

		ulong sum        = (ulong)Current + value;
		uint  newCurrent = sum >= Max ? Max : (uint)sum;
		return new(Max, newCurrent);
	}

	/// <summary>
	///     Returns a new range with the current value set, clamped to max.
	/// </summary>
	public UIntRange SetCurrent(uint value) => new(Max, value);

	/// <summary>
	///     Returns a new range with the maximum value set and current updated based on the chosen mode.
	/// </summary>
	/// <param name="value">The new maximum value.</param>
	/// <param name="preservePercentage">
	///     Whether to preserve the current percentage relative to the previous max.
	/// </param>
	public UIntRange SetMax(uint value, bool preservePercentage) =>
		preservePercentage
			? SetMaxPreservePercentage(value)
			: SetMaxClampCurrent(value);

	/// <summary>
	///     Returns a new range with the maximum value set and current clamped to the new max.
	/// </summary>
	/// <param name="value">The new maximum value.</param>
	public UIntRange SetMaxClampCurrent(uint value)
	{
		uint newCurrent = Current > value ? value : Current;
		return new(value, newCurrent);
	}

	/// <summary>
	///     Returns a new range with the maximum value set while preserving the current percentage.
	/// </summary>
	/// <param name="value">The new maximum value.</param>
	public UIntRange SetMaxPreservePercentage(uint value)
	{
		uint oldMax     = Max;
		uint newMax     = value;
		uint newCurrent = Current;

		if (oldMax > 0)
		{
			float percent = (float)Current / oldMax;
			var   scaled  = (uint)MathF.Round(percent * newMax, MidpointRounding.AwayFromZero);
			newCurrent = scaled > newMax ? newMax : scaled;
		}
		else if (newCurrent > newMax)
		{
			newCurrent = newMax;
		}

		return new(newMax, newCurrent);
	}

	private static uint Saturate(ulong value) => value >= uint.MaxValue ? uint.MaxValue : (uint)value;
}
