using System.Collections.Generic;
using Bezoro.GameSystems.DamageSystem.Abstractions;
using Bezoro.GameSystems.HealthSystem.Abstractions;

namespace Bezoro.GameSystems.Tests.DamageSystem;

internal sealed class TestDamageable<THealth>(THealth health) : IDamageable<THealth>
	where THealth : struct, IDamageableHealth<THealth>
{
	private readonly object _gate = new();
	private THealth _health = health;

	public THealth Health
	{
		get
		{
			lock (_gate)
				return _health;
		}
	}

	public bool TryUpdateHealth(THealth expected, THealth updated)
	{
		lock (_gate)
		{
			if (!EqualityComparer<THealth>.Default.Equals(_health, expected))
				return false;

			_health = updated;
			return true;
		}
	}
}
