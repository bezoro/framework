using System;
using Bezoro.GameSystems.DamageSystem.Abstractions;
using Bezoro.GameSystems.DamageSystem.Core;
using Bezoro.GameSystems.DamageSystem.Types;

namespace Bezoro.GameSystems.DamageSystem.Resistances;

/// <summary>
///     Applies resistances from an <see cref="IDamageResistanceProvider" />.
/// </summary>
public sealed class DamageResistanceRule : IDamageRule
{
	private readonly IDamageResistanceProvider _provider;

	/// <summary>
	///     Creates a resistance rule using the specified provider.
	/// </summary>
	public DamageResistanceRule(IDamageResistanceProvider provider)
	{
		_provider = provider ?? throw new ArgumentNullException(nameof(provider));
	}

	/// <inheritdoc />
	public void Apply(DamageContext context)
	{
		if ((context.Request.Flags & DamageFlags.True) != 0)
			return;

		var components = context.Components;
		for (var i = 0; i < components.Count; i++)
		{
			var component = components[i];
			if (!_provider.TryGetResistance(component.Type, out var resistance))
				continue;

			float adjusted = resistance.Apply(component.Amount);
			components[i] = new(component.Type, adjusted);
		}
	}
}
