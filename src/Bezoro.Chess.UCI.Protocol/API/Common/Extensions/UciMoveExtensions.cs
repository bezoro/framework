using System.Collections.Generic;
using System.Collections.Immutable;

namespace Bezoro.Chess.UCI.Protocol.API.Common.Extensions;

/// <summary>
///     Extension methods for working with UCI move collections.
/// </summary>
public static class UciMoveExtensions
{
	/// <summary>
	///     Returns true when the move collection contains the supplied UCI move with ordinal matching.
	/// </summary>
	/// <param name="moves">Move sequence to search.</param>
	/// <param name="move">Move in UCI notation.</param>
	public static bool ContainsUciMove(this IEnumerable<string> moves, string move)
	{
		if (moves is null) throw new ArgumentNullException(nameof(moves));
		if (move is null) throw new ArgumentNullException(nameof(move));

		foreach (string candidate in moves)
		{
			if (string.Equals(candidate, move, StringComparison.Ordinal))
				return true;
		}

		return false;
	}

	/// <summary>
	///     Normalizes a move sequence to lowercase invariant UCI strings.
	/// </summary>
	/// <param name="moves">Move sequence to normalize.</param>
	/// <returns>Immutable normalized snapshot.</returns>
	public static ImmutableArray<string> NormalizeUciMoves(this IEnumerable<string> moves)
	{
		if (moves is null) throw new ArgumentNullException(nameof(moves));

		var builder = ImmutableArray.CreateBuilder<string>();
		foreach (string move in moves)
			builder.Add(move.ToLowerInvariant());

		return builder.ToImmutable();
	}
}
