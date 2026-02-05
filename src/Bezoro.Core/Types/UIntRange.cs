namespace Bezoro.Core.Types;

/// <summary>
///     Represents a bounded unsigned integer range with minimum, current, and maximum values.
/// </summary>
/// <remarks>
///     Immutable value type. <see cref="Min" /> is always less than or equal to <see cref="Max" />, and
///     <see cref="Current" /> is clamped to that inclusive range. Thread-safe.
/// </remarks>
public readonly record struct UIntRange
{
	/// <summary>
	///     Initializes a new range with the specified value as both max and current and a minimum of 0.
	/// </summary>
	/// <param name="value">The current and maximum value.</param>
	public UIntRange(uint value)
		: this(value, value) { }

	/// <summary>
	///     Initializes a new range with the specified maximum, current, and minimum value.
	/// </summary>
	/// <param name="max">
	///     The requested maximum value. If less than <paramref name="min" />, it is set to
	///     <paramref name="min" />.
	/// </param>
	/// <param name="current">The requested current value, clamped to the resulting min/max range.</param>
	/// <param name="min">The minimum value.</param>
	public UIntRange(uint max, uint current, uint min = 0u)
	{
		Min     = min;
		Max     = max < min ? min : max;
		Current = Clamp(current, Min, Max);
	}

	/// <summary>
	///     Gets the current value as a percentage of the maximum.
	/// </summary>
	/// <remarks>
	///     When <see cref="Max" /> is 0, the percentage is 0.
	/// </remarks>
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
	///     Gets the minimum value.
	/// </summary>
	public uint Min { get; }

	/// <summary>
	///     Returns a new range with the current value increased by the specified amount, capped at max.
	/// </summary>
	/// <param name="value">The amount to add to the current value.</param>
	/// <returns>A new <see cref="UIntRange" /> with an updated current value.</returns>
	public UIntRange AddToCurrent(uint value)
	{
		if (value == 0) return this;

		ulong sum        = (ulong)Current + value;
		uint  newCurrent = sum >= Max ? Max : (uint)sum;
		return new(Max, newCurrent, Min);
	}

	/// <summary>
	///     Returns a new range with the maximum value decreased, clamped to min, and current updated accordingly.
	/// </summary>
	/// <param name="value">The amount to subtract from the maximum value.</param>
	/// <param name="mode">How to update the current value relative to the new maximum.</param>
	/// <returns>A new <see cref="UIntRange" /> with the updated maximum and current values.</returns>
	public UIntRange DecreaseMax(uint value, MaxValueUpdateMode mode)
	{
		if (value == 0) return this;

		uint maxSpan = Max - Min;
		uint newMax  = value >= maxSpan ? Min : Max - value;
		return SetMax(newMax, mode);
	}

	/// <summary>
	///     Returns a new range with the maximum value increased, saturated at <see cref="uint.MaxValue" />, and current
	///     updated accordingly.
	/// </summary>
	/// <param name="value">The amount to add to the maximum value.</param>
	/// <param name="mode">How to update the current value relative to the new maximum.</param>
	/// <returns>A new <see cref="UIntRange" /> with the updated maximum and current values.</returns>
	public UIntRange IncreaseMax(uint value, MaxValueUpdateMode mode)
	{
		if (value == 0) return this;

		uint newMax = Saturate((ulong)Max + value);
		return SetMax(newMax, mode);
	}

	/// <summary>
	///     Returns a new range with the current value set to max.
	/// </summary>
	/// <returns>A new <see cref="UIntRange" /> with the current value set to <see cref="Max" />.</returns>
	public UIntRange MaximizeCurrent() => new(Max, Max, Min);

	/// <summary>
	///     Returns a new range with the current value set, clamped to min and max.
	/// </summary>
	/// <param name="value">The requested current value.</param>
	/// <returns>A new <see cref="UIntRange" /> with the updated current value.</returns>
	public UIntRange SetCurrent(uint value) => new(Max, value, Min);

	/// <summary>
	///     Returns a new range with the current value set to min.
	/// </summary>
	/// <returns>A new <see cref="UIntRange" /> with the current value set to <see cref="Min" />.</returns>
	public UIntRange SetCurrentToMinimum() => new(Max, Min, Min);

	/// <summary>
	///     Returns a new range with the maximum value set and current updated based on the chosen mode.
	/// </summary>
	/// <param name="value">The new maximum value.</param>
	/// <param name="mode">How to update the current value relative to the new maximum.</param>
	/// <returns>A new <see cref="UIntRange" /> with the updated maximum and current values.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	///     Thrown when <paramref name="mode" /> is not a defined <see cref="MaxValueUpdateMode" /> value.
	/// </exception>
	public UIntRange SetMax(uint value, MaxValueUpdateMode mode)
	{
		uint newMax = value < Min ? Min : value;

		return mode switch
		{
			MaxValueUpdateMode.PreservePercentage => SetMaxPreservePercentage(newMax),
			MaxValueUpdateMode.ClampCurrent => SetMaxClampCurrent(newMax),
			_ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Invalid MaxValueUpdateMode")
		};
	}

	/// <summary>
	///     Returns a new range with the current value decreased by the specified amount, clamped to min.
	/// </summary>
	/// <param name="value">The amount to subtract from the current value.</param>
	/// <returns>A new <see cref="UIntRange" /> with the updated current value.</returns>
	public UIntRange SubtractFromCurrent(uint value)
	{
		if (value == 0 || Current <= Min) return this;

		uint deltaToMin = Current - Min;
		uint newCurrent = value >= deltaToMin ? Min : Current - value;
		return new(Max, newCurrent, Min);
	}

	private static uint Clamp(uint value, uint min, uint max)
	{
		if (value < min) return min;

		return value > max ? max : value;
	}

	private static uint Saturate(ulong value) => value >= uint.MaxValue ? uint.MaxValue : (uint)value;

	private UIntRange SetMaxClampCurrent(uint newMax)
	{
		uint newCurrent = Clamp(Current, Min, newMax);
		return new(newMax, newCurrent, Min);
	}

	private UIntRange SetMaxPreservePercentage(uint newMax)
	{
		uint oldMax     = Max;
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

		newCurrent = Clamp(newCurrent, Min, newMax);
		return new(newMax, newCurrent, Min);
	}
}
