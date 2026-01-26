using System.Collections.Generic;
using Bezoro.GameSystems.DamageSystem.Abstractions;
using Bezoro.GameSystems.DamageSystem.Types;

namespace Bezoro.GameSystems.DamageSystem.Core;

/// <summary>
///     Mutable context passed through damage rules.
/// </summary>
public sealed class DamageContext
{
	private readonly List<DamageComponent> _components;

	internal DamageContext(
		DamageRequest         request,
		IDamageable           target,
		uint                  healthBefore,
		List<DamageComponent> components)
	{
		Request          = request;
		Target           = target;
		HealthBefore     = healthBefore;
		_components      = components;
		GlobalMultiplier = 1f;
	}

	/// <summary>
	///     Gets the original damage request.
	/// </summary>
	public DamageRequest Request { get; }

	/// <summary>
	///     Gets the damageable target receiving damage.
	/// </summary>
	public IDamageable Target { get; }

	/// <summary>
	///     Gets the mutable component list.
	/// </summary>
	public IList<DamageComponent> Components => _components;

	/// <summary>
	///     Gets the health value before damage was applied.
	/// </summary>
	public uint HealthBefore { get; }

	/// <summary>
	///     Gets whether the damage has been cancelled by a rule.
	/// </summary>
	public bool IsCancelled { get; private set; }

	/// <summary>
	///     Gets or sets a flat bonus added to the total.
	/// </summary>
	public float GlobalFlatBonus { get; set; }

	/// <summary>
	///     Gets or sets a multiplier applied to the total.
	/// </summary>
	public float GlobalMultiplier { get; set; }

	/// <summary>
	///     Adds to the global flat bonus.
	/// </summary>
	public void AddFlatBonus(float flatBonus) => GlobalFlatBonus += flatBonus;

	/// <summary>
	///     Cancels the damage, resulting in zero applied damage.
	/// </summary>
	public void Cancel() => IsCancelled = true;

	/// <summary>
	///     Multiplies the current global multiplier.
	/// </summary>
	public void MultiplyAll(float multiplier) => GlobalMultiplier *= multiplier;
}
