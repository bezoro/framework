using System.Collections.Immutable;

namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents a parsed playable-match command from a textual input source.
/// </summary>
/// <param name="Kind">The parsed command kind.</param>
/// <param name="Move">The normalized UCI move, when <see cref="Kind" /> is <see cref="PlayableMatchCommandKind.Move" />.</param>
/// <param name="Fen">The parsed FEN, when <see cref="Kind" /> is <see cref="PlayableMatchCommandKind.LoadFen" />.</param>
/// <param name="Moves">The normalized played-move sequence following a loaded FEN, when applicable.</param>
/// <param name="Error">The validation error, when <see cref="Kind" /> is <see cref="PlayableMatchCommandKind.Invalid" />.</param>
public readonly record struct PlayableMatchCommand(
	PlayableMatchCommandKind Kind,
	string?                  Move  = null,
	Fen?                     Fen   = null,
	ImmutableArray<string>   Moves = default,
	string?                  Error = null
);
