using System.Collections.Immutable;

namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents the parsed payload of a UCI <c>info</c> line.
/// </summary>
public readonly record struct UciInfoPayload(
	uint?                 Depth,
	uint?                 SelDepth,
	uint?                 MultiPv,
	UciInfoScore?         Score,
	uint?                 Nodes,
	uint?                 Nps,
	uint?                 TbHits,
	uint?                 Time,
	uint?                 HashFull,
	uint?                 CpuLoad,
	string?               CurrentMove,
	uint?                 CurrentMoveNumber,
	ImmutableArray<string> Refutation,
	uint?                 CurrentLineCpu,
	ImmutableArray<string> CurrentLine,
	string?               String,
	PrincipalVariation?   PrincipalVariation
);
