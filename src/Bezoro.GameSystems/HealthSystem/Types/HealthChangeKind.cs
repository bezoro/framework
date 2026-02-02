namespace Bezoro.GameSystems.HealthSystem.Types;

/// <summary>
///     Describes the operation that caused a health change.
/// </summary>
public enum HealthChangeKind
{
	// Current
	DecreaseCurrent,
	IncreaseCurrent,
	RestoreCurrent,
	DepleteCurrent,
	FullyRestoreCurrent,
	SetCurrent,
	// Max
	DecreaseMax,
	IncreaseMax,
	SetMax,
	// Excess
	DepleteExcess,
	DecreaseExcess,
	IncreaseExcess,
	SetExcess
}
