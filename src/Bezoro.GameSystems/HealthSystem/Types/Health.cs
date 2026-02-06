using Bezoro.Core.Types;

namespace Bezoro.GameSystems.HealthSystem.Types;

/// <summary>
///     Default health implementation.
/// </summary>
/// <remarks>
///     Immutable value type. Operations return updated instances.
/// </remarks>
public readonly record struct Health
{
	private readonly UIntRange _range;

	/// <summary>
	///     Initializes a new health instance with current set to max.
	/// </summary>
	/// <param name="max">The maximum health.</param>
	public Health(uint max) : this(max, max) { }

	/// <summary>
	///     Initializes a new health instance.
	/// </summary>
	/// <param name="max">The maximum health.</param>
	/// <param name="current">The current health.</param>
	public Health(uint max, uint current)
	{
		_range = new(max, current);
	}

	private Health(UIntRange range)
	{
		_range = range;
	}

	/// <summary>
	///     Gets whether the current health is empty.
	/// </summary>
	public bool IsEmpty => _range.Current == _range.Min;

	/// <summary>
	///     Gets whether the current health is full.
	/// </summary>
	public bool IsFull => _range.Current == _range.Max;

	/// <summary>
	///     Gets the current health as a percentage of max.
	/// </summary>
	public Percent Percentage => _range.Percentage;

	/// <summary>
	///     Gets the current health value.
	/// </summary>
	public uint Current => _range.Current;

	/// <summary>
	///     Gets the maximum health value.
	/// </summary>
	public uint Max => _range.Max;

	/// <summary>
	///     Returns a new health with current decreased by the specified amount, clamped to zero.
	/// </summary>
	/// <param name="value">The amount to subtract from current.</param>
	/// <returns>The updated health.</returns>
	public Health DecreaseCurrentHealthBy(uint value) => new(_range.SubtractFromCurrent(value));

	/// <summary>
	///     Returns a new health with max decreased and current updated based on the chosen mode.
	/// </summary>
	/// <param name="value">The amount to subtract from max.</param>
	/// <param name="mode">How to update current relative to the new max.</param>
	/// <returns>The updated health.</returns>
	public Health DecreaseMaxHealthBy(uint value, MaxValueUpdateMode mode) =>
		new(_range.DecreaseMax(value, mode));

	/// <summary>
	///     Returns a new health with current set to zero.
	/// </summary>
	/// <returns>The updated health.</returns>
	public Health DepleteCurrentHealth() => new(_range.SetCurrentToMinimum());

	/// <summary>
	///     Returns a new health with current fully restored to max.
	/// </summary>
	/// <returns>The updated health.</returns>
	public Health FullyRestoreCurrentHealth() => new(_range.MaximizeCurrent());

	/// <summary>
	///     Returns a new health with max increased and current updated based on the chosen mode.
	/// </summary>
	/// <param name="value">The amount to add to max.</param>
	/// <param name="mode">How to update current relative to the new max.</param>
	/// <returns>The updated health.</returns>
	public Health IncreaseMaxHealthBy(uint value, MaxValueUpdateMode mode) =>
		new(_range.IncreaseMax(value, mode));

	/// <summary>
	///     Returns a new health with current restored by the specified amount, capped at max.
	/// </summary>
	/// <param name="value">The amount to restore.</param>
	/// <returns>The updated health.</returns>
	public Health RestoreCurrentHealthBy(uint value) => new(_range.AddToCurrent(value));

	/// <summary>
	///     Returns a new health with current set to the specified value, clamped to max.
	/// </summary>
	/// <param name="value">The new current health.</param>
	/// <returns>The updated health.</returns>
	public Health SetCurrentHealthTo(uint value) => new(_range.SetCurrent(value));

	/// <summary>
	///     Returns a new health with max set and current updated based on the chosen mode.
	/// </summary>
	/// <param name="value">The new maximum health.</param>
	/// <param name="mode">How to update current relative to the new max.</param>
	/// <returns>The updated health.</returns>
	public Health SetMaxHealthTo(uint value, MaxValueUpdateMode mode) =>
		new(_range.SetMax(value, mode));
}
