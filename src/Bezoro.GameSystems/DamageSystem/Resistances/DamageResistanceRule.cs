using System;
using Bezoro.GameSystems.DamageSystem.Abstractions;
using Bezoro.GameSystems.DamageSystem.Types;
using Bezoro.GameSystems.HealthSystem.Abstractions;

namespace Bezoro.GameSystems.DamageSystem.Resistances;

/// <summary>
///     Applies resistances from an <see cref="IDamageResistanceProvider" />.
/// </summary>
public sealed class DamageResistanceRule<THealth> : IDamageRule<THealth>
	where THealth : struct, IDamageableHealth<THealth>
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
	public DamageContext<THealth> Apply(DamageContext<THealth> context)
	{
		if ((context.Request.Flags & DamageFlags.True) != 0)
			return context;

		var components = context.Components;
		DamageComponent[]? updated = null;
		for (var i = 0; i < components.Count; i++)
		{
			var component = components[i];
			if (!_provider.TryGetResistance(component.Type, out var resistance))
			{
				if (updated is not null)
					updated[i] = component;

				continue;
			}

			if (updated is null)
			{
				updated = new DamageComponent[components.Count];
				for (var j = 0; j < i; j++)
					updated[j] = components[j];
			}

			float adjusted = resistance.Apply(component.Amount);
			updated[i] = new(component.Type, adjusted);
		}

		return updated is null ? context : context with { Components = updated };
	}
}
