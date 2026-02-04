using Bezoro.GameSystems.DamageSystem.Abstractions;
using Bezoro.GameSystems.DamageSystem.Services;
using Bezoro.GameSystems.DamageSystem.Types;
using Bezoro.GameSystems.HealthSystem.Abstractions;
using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.DamageSystem;

[TestSubject(typeof(DamageResolver<>))]
public class DamageResolverRuleApplicationTests
{
	[Fact]
	public void Resolve_WhenRuleReplacesComponents_ShouldUseUpdatedComponents()
	{
		var target = new TestDamageable<HealthWithExcess>(new(100u, 100u));
		var request = DamageRequest.FromComponents(new[]
		{
			new DamageComponent(DamageType.Fire, 4f),
			new DamageComponent(DamageType.Ice, 6f)
		});

		var rules = new IDamageRule<HealthWithExcess>[]
		{
			new ReplaceComponentsRule<HealthWithExcess>(1f)
		};

		var resolver = new DamageResolver<HealthWithExcess>(new(rules));

		var result = resolver.Resolve(request, target);

		result.IntendedDamage.Should().Be(2u);
		result.Components.Should().HaveCount(2);
		result.Components.Should().OnlyContain(component => component.Amount == 1f);
	}

	private sealed class ReplaceComponentsRule<THealth> : IDamageRule<THealth>
		where THealth : struct, IDamageableHealth<THealth>
	{
		private readonly float _amount;

		public ReplaceComponentsRule(float amount) => _amount = amount;

		public DamageContext<THealth> Apply(DamageContext<THealth> context)
		{
			var components = context.Components;
			var updated = new DamageComponent[components.Count];
			for (var i = 0; i < components.Count; i++)
				updated[i] = new(components[i].Type, _amount);

			return context with { Components = updated };
		}
	}
}
