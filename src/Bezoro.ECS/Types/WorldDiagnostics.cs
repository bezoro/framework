namespace Bezoro.ECS.Types;

/// <summary>
/// Point-in-time diagnostics for <see cref="Services.World" /> memory arenas.
/// </summary>
public sealed class WorldDiagnostics(
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

