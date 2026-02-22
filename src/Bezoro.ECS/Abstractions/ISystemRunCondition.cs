using Bezoro.ECS.Types;

namespace Bezoro.ECS.Abstractions;

/// <summary>
///     Evaluates whether a system should run for the current scheduler pass.
/// </summary>
public interface ISystemRunCondition
{
	/// <summary>
	///     Returns <c>true</c> when the associated system should run.
	/// </summary>
	/// <param name="context">Current scheduler context for the run-condition evaluation.</param>
	/// <returns><c>true</c> to run; otherwise <c>false</c>.</returns>
	bool ShouldRun(in SystemRunConditionContext context);
}
