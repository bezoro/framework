namespace Bezoro.ECS.Types;

/// <summary>
/// Point-in-time diagnostics for <see cref="Services.WorldV2" /> memory arenas.
/// </summary>
public sealed class WorldV2Diagnostics(
	ArenaDiagnostics entityArena,
	ArenaDiagnostics componentTypeArena,
	ArenaDiagnostics queryResultArena
)
{
	/// <summary>
	/// Entity slot diagnostics.
	/// </summary>
	public ArenaDiagnostics EntityArena { get; } = entityArena;

	/// <summary>
	/// Registered component type diagnostics.
	/// </summary>
	public ArenaDiagnostics ComponentTypeArena { get; } = componentTypeArena;

	/// <summary>
	/// Query result buffer diagnostics.
	/// </summary>
	public ArenaDiagnostics QueryResultArena { get; } = queryResultArena;
}
