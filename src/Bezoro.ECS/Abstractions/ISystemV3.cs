using Bezoro.ECS.Types;

namespace Bezoro.ECS.Abstractions;

/// <summary>
/// Defines a system contract for <see cref="Services.WorldV3" /> execution.
/// </summary>
public interface ISystemV3
{
	/// <summary>
	/// Executes this system update using the provided context.
	/// </summary>
	/// <param name="context">Update context containing world access and a deferred command stream.</param>
	void Update(in SystemContextV3 context);
}
