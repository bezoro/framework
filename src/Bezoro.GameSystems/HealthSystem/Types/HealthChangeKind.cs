namespace Bezoro.GameSystems.HealthSystem.Types;

/// <summary>
///     Describes the operation that caused a health change.
/// </summary>
public enum HealthChangeKind
{
	DecreaseCurrent,
	IncreaseCurrent,
	RestoreCurrent,
	DepleteCurrent,
	FullyRestoreCurrent,
	DecreaseMax,
	IncreaseMax,
	SetCurrent,
	SetMax,
	ClearExcess,
	DecreaseExcess,
	IncreaseExcess,
	SetExcess,
}
