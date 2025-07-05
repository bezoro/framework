namespace Bezoro.UCI.API.Types;

/// <summary>
///     Represents engine identification information.
/// </summary>
public readonly record struct EngineId
{
	public string? Author { get; init; }
	public string? Name   { get; init; }

	public override string ToString() =>
		$"{Name ?? "Unknown Engine"}" + (Author != null ? $" by {Author}" : "");
}
