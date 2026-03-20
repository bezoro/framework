using System.Collections.Generic;
using Bezoro.Chess.UCI.Protocol.Domain.Common.Constants;

namespace Bezoro.Chess.UCI.Protocol.Domain.EngineClient;

/// <summary>
///     Provides helpers for constructing UCI command lines and validating move strings.
/// </summary>
internal static class UciCommandBuilder
{
	/// <summary>
	///     Validates whether the supplied string is a well-formed UCI move.
	/// </summary>
	public static bool IsUciMoveString(string s)
	{
		if (string.IsNullOrEmpty(s)) return false;

		var span = s.AsSpan();
		if ((uint)span.Length - 4 > 1) return false;

		if (!IsFile(span[0]) || !IsRank(span[1]) || !IsFile(span[2]) || !IsRank(span[3])) return false;

		return span.Length == 4 || IsPromo(span[4]);
	}

	/// <summary>
	///     Normalizes a collection of user-provided search moves, keeping only valid UCI move strings.
	/// </summary>
	public static IReadOnlyList<string> NormalizeSearchMoves(IEnumerable<string>? moves)
	{
		if (moves is null) return Array.Empty<string>();

		List<string>? normalized = null;
		foreach (string? move in moves)
		{
			string? candidate = NormalizeMove(move);
			if (candidate is null) continue;

			normalized ??= new();
			normalized.Add(candidate);
		}

		return normalized ?? (IReadOnlyList<string>)Array.Empty<string>();
	}

	public static string BuildDebugCommand(bool enabled) =>
		$"{UciConstants.Commands.DEBUG} {(enabled ? UciConstants.Keywords.ON : UciConstants.Keywords.OFF)}";

	/// <summary>
	///     Builds a UCI-compliant "go ..." command from <paramref name="parameters" />.
	/// </summary>
	public static string BuildGoCommand(SearchParameters parameters)
	{
		var parts = new List<string> { UciConstants.Commands.GO };

		if (parameters.Ponder) parts.Add(UciConstants.Parameters.PONDER);
		if (parameters.Infinite) parts.Add(UciConstants.Parameters.INFINITE);
		if (parameters.WhiteTimeMs.HasValue)
			parts.Add($"{UciConstants.Parameters.WHITE_TIME} {parameters.WhiteTimeMs.Value}");

		if (parameters.BlackTimeMs.HasValue)
			parts.Add($"{UciConstants.Parameters.BLACK_TIME} {parameters.BlackTimeMs.Value}");

		if (parameters.WhiteIncrementMs.HasValue)
			parts.Add($"{UciConstants.Parameters.WHITE_TIME_INCREMENT} {parameters.WhiteIncrementMs.Value}");

		if (parameters.BlackIncrementMs.HasValue)
			parts.Add($"{UciConstants.Parameters.BLACK_TIME_INCREMENT} {parameters.BlackIncrementMs.Value}");

		if (parameters.MovesToGo is > 0)
			parts.Add($"{UciConstants.Parameters.MOVES_TO_GO} {parameters.MovesToGo.Value}");

		if (parameters.MoveTimeMs.HasValue)
			parts.Add($"{UciConstants.Parameters.MOVE_TIME} {parameters.MoveTimeMs.Value}");

		if (parameters.Nodes.HasValue) parts.Add($"{UciConstants.Parameters.NODES} {parameters.Nodes.Value}");
		if (parameters.Depth.HasValue) parts.Add($"{UciConstants.Parameters.DEPTH} {parameters.Depth.Value}");
		if (parameters.Mate.HasValue) parts.Add($"{UciConstants.Parameters.MATE} {parameters.Mate.Value}");

		bool hasAnyLimit =
			parameters.Infinite ||
			parameters.Depth.HasValue ||
			parameters.Mate.HasValue ||
			parameters.MoveTimeMs.HasValue ||
			parameters.Nodes.HasValue ||
			parameters.WhiteTimeMs.HasValue ||
			parameters.BlackTimeMs.HasValue;

		if (!hasAnyLimit)
			parts.Add($"{UciConstants.Parameters.DEPTH} 6");

		var searchMoves = NormalizeSearchMoves(parameters.SearchMoves);
		if (searchMoves.Count > 0)
			parts.Add($"{UciConstants.Parameters.SEARCH_MOVES} " + string.Join(' ', searchMoves));

		return string.Join(' ', parts);
	}

	public static string BuildPositionCommand(Fen fen, IEnumerable<string>? moves)
	{
		string positionTarget = string.Equals(fen.Raw, Fen.Default.Raw, StringComparison.Ordinal)
									? UciConstants.Commands.START_POS
									: $"{UciConstants.Keywords.FEN} {fen.Raw}";

		var normalizedMoves = NormalizeSearchMoves(moves);
		if (normalizedMoves.Count == 0)
			return $"{UciConstants.Commands.POSITION} {positionTarget}";

		return
			$"{UciConstants.Commands.POSITION} {positionTarget} {UciConstants.Keywords.MOVES} {string.Join(' ', normalizedMoves)}";
	}

	public static string BuildRegisterCommand(UciRegistration registration)
	{
		if (registration.Later)
			return $"{UciConstants.Commands.REGISTER} {UciConstants.Keywords.LATER}";

		if (string.IsNullOrWhiteSpace(registration.Name))
			throw new ArgumentException(
				"A registration name is required unless 'register later' is requested.",
				nameof(registration)
			);

		var parts = new List<string>
		{
			UciConstants.Commands.REGISTER,
			UciConstants.Keywords.NAME,
			registration.Name.Trim()
		};

		if (!string.IsNullOrWhiteSpace(registration.Code))
		{
			parts.Add(UciConstants.Keywords.CODE);
			parts.Add(registration.Code.Trim());
		}

		return string.Join(' ', parts);
	}

	public static string BuildSetOptionCommand(string name, string? value) =>
		value is null
			? $"{UciConstants.Commands.SET_OPTION} {UciConstants.Keywords.NAME} {name}"
			: $"{UciConstants.Commands.SET_OPTION} {UciConstants.Keywords.NAME} {name} {UciConstants.Keywords.VALUE} {value}";

	/// <summary>
	///     Attempts to normalize a UCI move string.
	/// </summary>
	public static string? NormalizeMove(string? move)
	{
		if (string.IsNullOrWhiteSpace(move)) return null;

		return IsUciMoveString(move) ? move.ToLowerInvariant() : null;
	}

	/// <summary>
	///     Extracts valid UCI moves from a line and adds them to <paramref name="destination" />.
	/// </summary>
	public static void CollectMovesFromLine(string line, ISet<string> destination)
	{
		if (destination is null) throw new ArgumentNullException(nameof(destination));

		if (string.IsNullOrEmpty(line)) return;

		var span  = line.AsSpan();
		var index = 0;
		while (index < span.Length)
		{
			while (index < span.Length && IsSeparator(span[index])) index++;

			int start = index;
			while (index < span.Length && !IsSeparator(span[index])) index++;

			int length = index - start;
			if ((uint)(length - 4) > 1) continue;

			var token = span.Slice(start, length);
			if (IsUciMoveSpan(token))
				destination.Add(new(token));
		}
	}

	private static bool IsFile(char c) => (uint)((c | 0x20) - 'a') <= 7;

	private static bool IsPromo(char c)
	{
		int lc = c | 0x20;
		return lc is 'q' or 'r' or 'b' or 'n';
	}

	private static bool IsRank(char c) => (uint)(c - '1') <= 7;

	private static bool IsSeparator(char c) =>
		c is ' ' or '\t' or ',' or ';' or '|' or ':' or '\r' or '\n';

	private static bool IsUciMoveSpan(ReadOnlySpan<char> span)
	{
		if ((uint)span.Length - 4 > 1) return false;
		if (!IsFile(span[0]) || !IsRank(span[1]) || !IsFile(span[2]) || !IsRank(span[3])) return false;

		return span.Length == 4 || IsPromo(span[4]);
	}
}
