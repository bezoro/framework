using System;
using Bezoro.GameSystems.DamageSystem.Abstractions;
using Bezoro.GameSystems.DamageSystem.Types;

namespace Bezoro.GameSystems.DamageSystem.Rules;

/// <summary>
///     Applies a multiplier when <see cref="DamageFlags.Critical" /> is set.
/// </summary>
public sealed class CriticalDamageRule : IDamageRule
{
	private readonly float _multiplier;

	/// <summary>
	///     Creates a critical damage rule with the specified multiplier.
	/// </summary>
	/// <param name="multiplier">Multiplier applied when a hit is critical.</param>
	public CriticalDamageRule(float multiplier = 1.5f)
	{
		if (multiplier <= 0f)
			throw new ArgumentOutOfRangeException(nameof(multiplier), "Critical multiplier must be > 0.");

		_multiplier = multiplier;
	}

	/// <inheritdoc />
	public void Apply(DamageContext context)
	{
		if ((context.Request.Flags & DamageFlags.Critical) == 0)
			return;

		context.GlobalMultiplier *= _multiplier;
	}
}
