using System.Collections.Generic;

namespace Bezoro.UCI.API.Types;

/// <summary>
///     Represents a UCI engine option.
/// </summary>
public readonly record struct EngineOption
{
	public          IReadOnlyList<string>? Variables    { get; init; }
	public          string?                DefaultValue { get; init; }
	public          string?                MaxValue     { get; init; }
	public          string?                MinValue     { get; init; }
	public          string?                Name         { get; init; }
	public          string?                Type         { get; init; }
	public override string                 ToString()   => $"{Name} ({Type})";
}
