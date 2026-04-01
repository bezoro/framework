namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents a concrete piece involved in a protocol-side playable match event.
/// </summary>
/// <param name="Color">Piece color: <c>w</c> or <c>b</c>.</param>
/// <param name="Kind">Lowercase piece kind: <c>p</c>, <c>n</c>, <c>b</c>, <c>r</c>, <c>q</c>, or <c>k</c>.</param>
/// <param name="Symbol">Original FEN symbol preserving color casing.</param>
public readonly record struct PlayableMatchPiece(
	char Color,
	char Kind,
	char Symbol
);
