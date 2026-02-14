using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Internal.Fixed;

namespace Bezoro.ECS.Types;

/// <summary>
/// Immutable handle for a compiled query specification.
/// </summary>
/// <typeparam name="TSpec">Query specification type.</typeparam>
public readonly struct QueryHandle<TSpec> where TSpec : struct, ICompiledQuerySpec
{
	internal QueryHandle(CompiledQueryPlan plan)
	{
		Plan = plan ?? throw new ArgumentNullException(nameof(plan));
	}

	internal CompiledQueryPlan Plan { get; }
}
