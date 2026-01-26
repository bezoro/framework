using System.Collections.Generic;
using Bezoro.GameSystems.DamageSystem.Abstractions;
using Bezoro.GameSystems.DamageSystem.Types;

namespace Bezoro.GameSystems.DamageSystem.Resistances;

/// <summary>
///     A simple resistance table backed by a dictionary.
/// </summary>
public sealed class DamageResistanceTable : IDamageResistanceProvider
{
	private readonly Dictionary<DamageType, DamageResistance> _resistances;

	/// <summary>
	///     Creates an empty resistance table.
	/// </summary>
	public DamageResistanceTable() : this(null) { }

	/// <summary>
	///     Creates an empty resistance table with a custom comparer.
	/// </summary>
	public DamageResistanceTable(IEqualityComparer<DamageType>? comparer)
	{
		_resistances = new(comparer ?? EqualityComparer<DamageType>.Default);
	}

	/// <summary>
	///     Gets or sets the resistance for a damage type.
	/// </summary>
	public DamageResistance this[DamageType type]
	{
		get => _resistances.TryGetValue(type, out var value) ? value : DamageResistance.None;
		set => _resistances[type] = value;
	}

	/// <summary>
	///     Removes a resistance entry.
	/// </summary>
	public bool Remove(DamageType type)
		=> _resistances.Remove(type);

	/// <inheritdoc />
	public bool TryGetResistance(DamageType type, out DamageResistance resistance)
		=> _resistances.TryGetValue(type, out resistance);

	/// <summary>
	///     Clears all resistance entries.
	/// </summary>
	public void Clear()
		=> _resistances.Clear();

	/// <summary>
	///     Sets the resistance for a damage type.
	/// </summary>
	public void Set(DamageType type, DamageResistance resistance)
		=> _resistances[type] = resistance;
}
