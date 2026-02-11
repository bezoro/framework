using Bezoro.Core.Types;

namespace Bezoro.GameSystems.HealthSystem.Types;

/// <summary>
///     Health component used by the ECS health simulation.
/// </summary>
public struct Health
{
	/// <summary>
	///     Initializes a health component.
	/// </summary>
	/// <param name="max">Base health cap.</param>
	/// <param name="current">Base health value.</param>
	/// <param name="excessCurrent">Excess health value.</param>
	/// <param name="excessMax">Excess health cap.</param>
	public Health(uint max, uint current, uint excessCurrent = 0u, uint excessMax = 0u)
	{
		Max = max;
		Current = current > max ? max : current;
		ExcessMax = excessMax;

		uint overflow = current > Max ? current - Max : 0u;
		ulong totalExcess = (ulong)excessCurrent + overflow;
		ExcessCurrent = ClampToExcessMax(totalExcess, excessMax);
	}

	/// <summary>
	///     Gets or sets current base health.
	/// </summary>
	public uint Current;

	/// <summary>
	///     Gets or sets current excess health.
	/// </summary>
	public uint ExcessCurrent;

	/// <summary>
	///     Gets or sets excess health cap.
	/// </summary>
	public uint ExcessMax;

	/// <summary>
	///     Gets or sets base health cap.
	/// </summary>
	public uint Max;

	/// <summary>
	///     Gets base health percentage.
	/// </summary>
	public readonly Percent BasePercentage => new(Current, Max);

	/// <summary>
	///     Gets effective health used for damage processing.
	/// </summary>
	public readonly uint EffectiveCurrent => Saturate(Current, ExcessCurrent);

	/// <summary>
	///     Gets excess health percentage.
	/// </summary>
	public readonly Percent ExcessPercentage => new(ExcessCurrent, ExcessMax);

	/// <summary>
	///     Gets whether all health pools are empty.
	/// </summary>
	public readonly bool IsEmpty => Current == 0u && ExcessCurrent == 0u;

	/// <summary>
	///     Gets whether all health pools are full.
	/// </summary>
	public readonly bool IsFull => Current == Max && ExcessCurrent == ExcessMax;

	/// <summary>
	///     Gets combined base + excess percentage.
	/// </summary>
	public readonly Percent TotalPercentage => Percent.FromTotals((Current, Max), (ExcessCurrent, ExcessMax));

	private static uint ClampToExcessMax(ulong value, uint excessMax)
	{
		if (excessMax == 0u || value == 0u)
			return 0u;

		return value >= excessMax ? excessMax : (uint)value;
	}

	private static uint Saturate(uint left, uint right)
	{
		ulong sum = (ulong)left + right;
		return sum >= uint.MaxValue ? uint.MaxValue : (uint)sum;
	}
}
