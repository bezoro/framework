using Bezoro.Core.Types;

namespace Bezoro.GameSystems.HealthSystem.Abstractions;

/// <summary>Defines a health contract with current and maximum values and related operations.</summary>
public interface IHealth
{
	/// <summary>Gets the health percentage derived from <see cref="Current" /> and <see cref="Max" />.</summary>
	public Percent Percentage { get; }

	/// <summary>Gets the current health value.</summary>
	public uint Current { get; }

	/// <summary>Gets the maximum health value.</summary>
	public uint Max { get; }

	/// <summary>Decreases the current health by the specified value.</summary>
	/// <param name="value">The amount to subtract from the current health.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	///     Thrown when the value would make the current health invalid for the
	///     implementation.
	/// </exception>
	public void DecreaseCurrentHealthBy(uint value);

	/// <summary>Decreases the maximum health by the specified value.</summary>
	/// <param name="value">The amount to subtract from the maximum health.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	///     Thrown when the value would make the maximum or current health invalid
	///     for the implementation.
	/// </exception>
	public void DecreaseMaxHealthBy(uint value);

	/// <summary>Sets the current health to its depleted value (typically zero).</summary>
	public void DepleteCurrentHealth();

	/// <summary>Restores the current health to its maximum value.</summary>
	public void FullyRestoreCurrentHealth();

	/// <summary>Increases the current health by the specified value.</summary>
	/// <param name="value">The amount to add to the current health.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	///     Thrown when the value would make the current health invalid for the
	///     implementation.
	/// </exception>
	public void IncreaseCurrentHealthBy(uint value);

	/// <summary>Increases the maximum health by the specified value.</summary>
	/// <param name="value">The amount to add to the maximum health.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	///     Thrown when the value would make the maximum or current health invalid
	///     for the implementation.
	/// </exception>
	public void IncreaseMaxHealthBy(uint value);

	/// <summary>Restores the current health by the specified value.</summary>
	/// <param name="value">The amount to restore to the current health.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	///     Thrown when the value would make the current health invalid for the
	///     implementation.
	/// </exception>
	public void RestoreCurrentHealthBy(uint value);

	/// <summary>Sets the current health to the specified value.</summary>
	/// <param name="value">The new current health value.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside the valid range for the implementation.</exception>
	public void SetCurrentHealthTo(uint value);

	/// <summary>Sets the maximum health to the specified value.</summary>
	/// <param name="value">The new maximum health value.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	///     Thrown when the value would make the maximum or current health invalid
	///     for the implementation.
	/// </exception>
	public void SetMaxHealthTo(uint value);
}
