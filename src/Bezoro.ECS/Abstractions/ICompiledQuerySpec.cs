using Bezoro.ECS.Types;

namespace Bezoro.ECS.Abstractions;

/// <summary>
///     Defines a compiled query specification for <see cref="Services.World" />.
/// </summary>
public interface ICompiledQuerySpec
{
	/// <summary>
	///     Builds the query requirements into the provided builder.
	/// </summary>
	/// <param name="builder">The mutable query builder.</param>
	void Build(ref QueryBuilder builder);
}
