using System.Collections.Generic;

namespace Bezoro.UCI.Domain.Commands;

internal record struct SearchCommand
{
	public bool                 Infinite    { get; private set; }
	public bool                 Ponder      { get; private set; }
	public IEnumerable<string>? SearchMoves { get; private set; }
	public uint                 Depth       { get; private set; }
	public uint                 TimeMs      { get; private set; }
	public uint?                BInc        { get; private set; }
	public uint?                BTime       { get; private set; }
	public uint?                MovesToGo   { get; private set; }
	public uint?                Nodes       { get; private set; }
	public uint?                WInc        { get; private set; }
	public uint?                WTime       { get; private set; }

	public static implicit operator string(SearchCommand command)
	{
		var parts = new List<string> { "go" };

		if (command.SearchMoves is not null)
		{
			var normalizedMoves = NormalizeMoves(command.SearchMoves);
			if (normalizedMoves.Count > 0)
				parts.Add($"searchmoves {string.Join(" ", normalizedMoves)}");
		}

		if (command.Ponder)
			parts.Add("ponder");

		if (command.WTime.HasValue)
			parts.Add($"wtime {command.WTime}");

		if (command.BTime.HasValue)
			parts.Add($"btime {command.BTime}");

		if (command.WInc.HasValue)
			parts.Add($"winc {command.WInc}");

		if (command.BInc.HasValue)
			parts.Add($"binc {command.BInc}");

		if (command.MovesToGo.HasValue)
			parts.Add($"movestogo {command.MovesToGo}");

		if (command.Depth > 0)
			parts.Add($"depth {command.Depth}");

		if (command.Nodes.HasValue)
			parts.Add($"nodes {command.Nodes}");

		if (command.TimeMs > 0)
			parts.Add($"movetime {command.TimeMs}");

		if (command.Infinite)
			parts.Add("infinite");

		return string.Join(" ", parts);
	}

	public SearchCommand WithDepth(uint depth)
	{
		Depth = depth;
		return this;
	}

	public SearchCommand WithInfinite()
	{
		Infinite = true;
		return this;
	}

	public SearchCommand WithMovesToGo(uint movesToGo)
	{
		MovesToGo = movesToGo;
		return this;
	}

	public SearchCommand WithNodes(uint nodes)
	{
		Nodes = nodes;
		return this;
	}

	public SearchCommand WithPonder()
	{
		Ponder = true;
		return this;
	}

	public SearchCommand WithSearchMoves(IEnumerable<string> moves)
	{
		SearchMoves = NormalizeMoves(moves);
		return this;
	}

	public SearchCommand WithTime(uint timeMs)
	{
		TimeMs = timeMs;
		return this;
	}

	public SearchCommand WithTimeControl(uint wtime, uint btime, uint? winc = null, uint? binc = null)
	{
		WTime = wtime;
		BTime = btime;
		WInc  = winc;
		BInc  = binc;
		return this;
	}

	private static List<string> NormalizeMoves(IEnumerable<string> moves)
	{
		var seen   = new HashSet<string>(StringComparer.Ordinal);
		var result = new List<string>();

		foreach (string? m in moves)
		{
			if (string.IsNullOrWhiteSpace(m)) continue;

			string move = m.Trim().ToLowerInvariant();
			if (move.Length < 4) continue;

			if (seen.Add(move))
				result.Add(move);
		}

		return result;
	}
}
