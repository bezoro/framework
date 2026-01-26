using System;
using System.Collections.Generic;
using Bezoro.GameSystems.DamageSystem.Abstractions;
using Bezoro.GameSystems.DamageSystem.Types;

namespace Bezoro.GameSystems.DamageSystem.Core;

/// <summary>
///     Default damage resolver that applies optional rules and updates health.
/// </summary>
public sealed class DamageResolver : IDamageResolver
{
	private static readonly IDamageRule[]        EmptyRules = Array.Empty<IDamageRule>();
	private readonly        DamageResolverConfig _config;
	private readonly        IDamageRule[]        _rules;

	/// <summary>
	///     Creates a resolver with the specified configuration.
	/// </summary>
	public DamageResolver(DamageResolverConfig config)
	{
		_config = config;
		_rules  = config.Rules is { Count: > 0 } ? CopyRules(config.Rules) : EmptyRules;
	}

	/// <summary>
	///     Gets a basic resolver with no additional rules.
	/// </summary>
	public static DamageResolver Basic { get; } = new(new());

	/// <inheritdoc />
	public DamageResult Resolve(in DamageRequest request, IDamageable target)
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));

		var  health       = target.Health;
		uint healthBefore = health.Current;
		var  components   = BuildComponents(request);

		var context = new DamageContext(request, target, healthBefore, components);
		for (var i = 0; i < _rules.Length; i++)
			_rules[i].Apply(context);

		if (context.IsCancelled)
			return new(
				healthBefore,
				health.Current,
				0,
				0,
				0f,
				components,
				true);

		float rawTotal = SumComponents(context.Components);
		rawTotal += request.FlatBonus + context.GlobalFlatBonus;
		rawTotal *= request.Multiplier * context.GlobalMultiplier;

		if (float.IsNaN(rawTotal) || float.IsInfinity(rawTotal))
			rawTotal = 0f;

		int  roundedDamage  = RoundDamage(rawTotal, _config.RoundingMode);
		uint intendedDamage = ClampToUInt(roundedDamage, _config.MinimumAppliedDamage, _config.MaximumAppliedDamage);

		if (_config.ClampToCurrentHealth && intendedDamage > healthBefore)
			intendedDamage = healthBefore;

		if (intendedDamage != 0)
			health.DecreaseCurrentHealthBy(intendedDamage);

		uint healthAfter   = health.Current;
		uint appliedDamage = healthBefore > healthAfter ? healthBefore - healthAfter : 0;

		return new(
			healthBefore,
			healthAfter,
			intendedDamage,
			appliedDamage,
			rawTotal,
			components,
			false);
	}

	private static float SumComponents(IList<DamageComponent> components)
	{
		var total = 0f;
		for (var i = 0; i < components.Count; i++)
			total += components[i].Amount;

		return total;
	}

	private static IDamageRule[] CopyRules(IReadOnlyList<IDamageRule> rules)
	{
		var array = new IDamageRule[rules.Count];
		for (var i = 0; i < rules.Count; i++)
			array[i] = rules[i];

		return array;
	}

	private static int RoundDamage(float value, DamageRoundingMode mode)
	{
		return mode switch
		{
			DamageRoundingMode.Floor       => (int)MathF.Floor(value),
			DamageRoundingMode.Ceiling     => (int)MathF.Ceiling(value),
			DamageRoundingMode.Truncate    => (int)value,
			DamageRoundingMode.RoundToEven => (int)MathF.Round(value, MidpointRounding.ToEven),
			_                              => (int)MathF.Round(value, MidpointRounding.AwayFromZero)
		};
	}

	private static List<DamageComponent> BuildComponents(in DamageRequest request)
	{
		if (request.Components is { Count: > 0 } components)
		{
			var list = new List<DamageComponent>(components.Count);
			for (var i = 0; i < components.Count; i++)
				list.Add(components[i]);

			return list;
		}

		return new(1)
		{
			new(request.Type, request.BaseAmount)
		};
	}

	private static uint ClampToUInt(int value, uint min, uint? max)
	{
		uint result = value <= 0 ? 0u : (uint)value;

		if (result < min)
			result = min;

		if (max.HasValue && result > max.Value)
			result = max.Value;

		return result;
	}
}
