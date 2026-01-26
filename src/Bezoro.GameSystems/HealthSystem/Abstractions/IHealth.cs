using Bezoro.Core.Types;

namespace Bezoro.GameSystems.HealthSystem.Abstractions;

public interface IHealth
{
	public Percent Percentage { get; }

	public uint Current { get; }
	public uint Max     { get; }

	public void DecreaseCurrentHealthBy(uint value);
	public void DecreaseMaxHealthBy(uint     value);

	public void DepleteCurrentHealth();
	public void FullyRestoreCurrentHealth();

	public void IncreaseCurrentHealthBy(uint value);
	public void IncreaseMaxHealthBy(uint     value);

	public void RestoreCurrentHealthBy(uint value);

	public void SetCurrentHealthTo(uint value);
	public void SetMaxHealthTo(uint     value);
}
