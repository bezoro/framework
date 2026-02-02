namespace Bezoro.GameSystems.HealthSystem.Abstractions;

/// <summary>
///     Optional interface for health implementations that support excess health.
/// </summary>
public interface IExcessHealth
{
	/// <summary>
	///     Gets the current excess health value.
	/// </summary>
	uint Excess { get; }

	/// <summary>
	///     Depletes all excess health.
	/// </summary>
	void DepleteExcessHealth();

	/// <summary>
	///     Decreases excess health by the given value.
	/// </summary>
	void DecreaseExcessHealthBy(uint value);

	/// <summary>
	///     Increases excess health by the given value.
	/// </summary>
	void IncreaseExcessHealthBy(uint value);

	/// <summary>
	///     Sets the excess health to the given value.
	/// </summary>
	void SetExcessHealthTo(uint value);
}
