using System;
using System.Collections.Generic;
using Bezoro.GameSystems.DamageSystem.Abstractions;
using Bezoro.GameSystems.DamageSystem.Types;
using Bezoro.GameSystems.HealthSystem.Abstractions;

namespace Bezoro.GameSystems.DamageSystem.Services;

/// <summary>
///     Default damage resolver that applies optional rules and updates health.
/// </summary>
public sealed class DamageResolver<THealth> : IDamageResolver<THealth>
	where THealth : struct, IDamageableHealth<THealth>
{
	private static readonly IDamageRule<THealth>[]        EmptyRules = Array.Empty<IDamageRule<THealth>>();
	private readonly        DamageResolverConfig<THealth> _config;
	private readonly        IDamageRule<THealth>[]        _rules;

	/// <summary>
	///     Creates a resolver with the specified configuration.
	/// </summary>
	public DamageResolver(DamageResolverConfig<THealth> config)
	{
		_config = config;
		_rules  = config.Rules is { Count: > 0 } ? CopyRules(config.Rules) : EmptyRules;
	}

	/// <summary>
	///     Gets a basic resolver with no additional rules.
	/// </summary>
	public static DamageResolver<THealth> Basic { get; } = new(new());

	/// <inheritdoc />
	public DamageResult Resolve(in DamageRequest request, IDamageable<THealth> target)
	{
		if (target is null)
			throw new ArgumentNullException(nameof(target));

		var context = CreateContext(request, target, out var components);
		ApplyRules(context);

		if (context.IsCancelled)
			return BuildCancelledResult(context.HealthBefore, components);

		float rawTotal       = CalculateRawDamage(context, request);
		uint  intendedDamage = CalculateIntendedDamage(rawTotal);

		(uint healthBefore, uint healthAfter, uint appliedIntended) =
			ApplyDamage(target, context.HealthBefore, intendedDamage);

		uint appliedDamage = CalculateAppliedDamage(healthBefore, healthAfter);

		return new(
			healthBefore,
			healthAfter,
			appliedIntended,
			appliedDamage,
			rawTotal,
			components,
			false
		);
	}

	private static DamageResult BuildCancelledResult(uint healthBefore, IReadOnlyList<DamageComponent> components) =>
		new(
			healthBefore,
			healthBefore,
			0,
			0,
			0f,
			components,
			true
		);

	private static float CalculateRawDamage(DamageContext<THealth> context, in DamageRequest request)
	{
		float rawTotal = SumComponents(context.Components);
		rawTotal += request.FlatBonus + context.GlobalFlatBonus;
		rawTotal *= request.Multiplier * context.GlobalMultiplier;

		return float.IsNaN(rawTotal) || float.IsInfinity(rawTotal) ? 0f : rawTotal;
	}

	private static float SumComponents(IList<DamageComponent> components)
	{
		var total = 0f;
		for (var i = 0; i < components.Count; i++)
			total += components[i].Amount;

		return total;
	}

	private static IDamageRule<THealth>[] CopyRules(IReadOnlyList<IDamageRule<THealth>> rules)
	{
		var array = new IDamageRule<THealth>[rules.Count];
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
		if (request.Components is not { Count: > 0 } components)
			return [new(request.Type, request.BaseAmount)];

		var list = new List<DamageComponent>(components.Count);
		for (var i = 0; i < components.Count; i++)
			list.Add(components[i]);

		return list;
	}

	private static uint CalculateAppliedDamage(uint healthBefore, uint healthAfter) =>
		healthBefore > healthAfter ? healthBefore - healthAfter : 0;

	private static uint ClampToUInt(int value, uint min, uint? max)
	{
		uint result = value <= 0 ? 0u : (uint)value;

		if (result < min)
			result = min;

		if (max.HasValue && result > max.Value)
			result = max.Value;

		return result;
	}

	private (uint HealthBefore, uint HealthAfter, uint AppliedIntended) ApplyDamage(
		IDamageable<THealth> target,
		uint                 healthBefore,
		uint                 intendedDamage)
	{
		if (intendedDamage == 0)
			return (healthBefore, healthBefore, 0);

		uint appliedIntended = intendedDamage;
		while (true)
		{
			var  snapshot      = target.Health;
			uint currentHealth = snapshot.EffectiveCurrent;
			uint clamped       = appliedIntended;

			if (_config.ClampToCurrentHealth && clamped > currentHealth)
				clamped = currentHealth;

			if (clamped == 0)
				return (currentHealth, currentHealth, 0);

			var updated = snapshot.ApplyDamage(clamped);
			if (!target.TryUpdateHealth(snapshot, updated))
				continue;

			return (currentHealth, updated.EffectiveCurrent, clamped);
		}
	}

	private DamageContext<THealth> CreateContext(
		in DamageRequest          request,
		IDamageable<THealth>      target,
		out List<DamageComponent> components)
	{
		components = BuildComponents(request);
		uint healthBefore = target.Health.EffectiveCurrent;

		return new(request, target, healthBefore, components);
	}

	private uint CalculateIntendedDamage(float rawTotal)
	{
		int roundedDamage = RoundDamage(rawTotal, _config.RoundingMode);

		return ClampToUInt(roundedDamage, _config.MinimumAppliedDamage, _config.MaximumAppliedDamage);
	}

	private void ApplyRules(DamageContext<THealth> context)
	{
		for (var i = 0; i < _rules.Length; i++)
			_rules[i].Apply(context);
	}
}
