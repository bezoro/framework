using Bezoro.GameSystems.DamageSystem.Resistances;
using Bezoro.GameSystems.DamageSystem.Types;

namespace Bezoro.GameSystems.DamageSystem.Abstractions;

/// <summary>
///     Provides resistance values for damage types.
/// </summary>
public interface IDamageResistanceProvider
{
	/// <summary>
	///     Attempts to retrieve a resistance for the given damage type.
	/// </summary>
	bool TryGetResistance(DamageType type, out DamageResistance resistance);
}
