using System.Collections.Generic;
using System.Collections.Immutable;
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;

namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Describes the complete authored or saved playable-match state to load into a session.
/// </summary>
public readonly record struct PlayableMatchSetup
{
	/// <summary>
	///     Creates a complete playable-match setup from a base FEN, optional played-move sequence, and optional clock
	///     values.
	/// </summary>
	/// <param name="baseFen">Base position before <paramref name="playedMoves" /> are applied.</param>
	/// <param name="playedMoves">Moves played from <paramref name="baseFen" />, in UCI notation.</param>
	/// <param name="clock">Optional clock values to restore while loading the setup.</param>
	public PlayableMatchSetup(
		Fen                       baseFen,
		IEnumerable<string>?      playedMoves = null,
		PlayableMatchClockSetup?  clock       = null)
	{
		if (string.IsNullOrWhiteSpace(baseFen.Raw))
			throw new ArgumentException("Base FEN must be a parsed FEN value.", nameof(baseFen));

		BaseFen     = baseFen;
		PlayedMoves = NormalizePlayedMoves(playedMoves);
		Clock       = clock;
	}

	/// <summary>
	///     Gets the standard initial match setup.
	/// </summary>
	public static PlayableMatchSetup Standard { get; } = new(Fen.Default);

	/// <summary>
	///     Gets the base position before <see cref="PlayedMoves" /> are applied.
	/// </summary>
	public Fen BaseFen { get; }

	/// <summary>
	///     Gets the normalized move sequence played from <see cref="BaseFen" />.
	/// </summary>
	public ImmutableArray<string> PlayedMoves { get; }

	/// <summary>
	///     Gets optional clock values to restore while loading the setup.
	/// </summary>
	public PlayableMatchClockSetup? Clock { get; }

	/// <summary>
	///     Resolves the effective current position after applying <see cref="PlayedMoves" /> to <see cref="BaseFen" />.
	/// </summary>
	/// <returns>The effective current position.</returns>
	/// <exception cref="ArgumentException">Thrown when any played move is illegal for its position.</exception>
	public Fen ResolveCurrentFen()
	{
		var current = BaseFen;
		foreach (string move in PlayedMoves)
		{
			try
			{
				current = current.ApplyMove(move);
			}
			catch (InvalidOperationException ex)
			{
				throw new ArgumentException(
					$"Played move '{move}' is illegal for its position in the setup.",
					nameof(PlayedMoves),
					ex);
			}
		}

		return current;
	}

	/// <summary>
	///     Validates the setup's move sequence and optional clock values.
	/// </summary>
	/// <exception cref="ArgumentException">Thrown when a played move is illegal for its position.</exception>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when a clock value is outside its valid range.</exception>
	public void Validate()
	{
		ResolveCurrentFen();
		Clock?.Validate();
	}

	private static ImmutableArray<string> NormalizePlayedMoves(IEnumerable<string>? playedMoves)
	{
		if (playedMoves is null)
			return [];

		var builder = ImmutableArray.CreateBuilder<string>();
		foreach (string move in playedMoves)
			builder.Add(NormalizeMove(move));

		return builder.ToImmutable();
	}

	private static string NormalizeMove(string move)
	{
		if (move is null)
			throw new ArgumentNullException(nameof(move));

		string normalizedMove = move.Trim().ToLowerInvariant();
		if (!IsUciMoveString(normalizedMove))
			throw new ArgumentException("Enter a move in UCI notation such as e2e4 or a7a8q.", nameof(move));

		return normalizedMove;
	}

	private static bool IsUciMoveString(string move)
	{
		if (move.Length is not 4 and not 5)
			return false;

		if (!IsFile(move[0]) ||
			!IsRank(move[1]) ||
			!IsFile(move[2]) ||
			!IsRank(move[3]))
		{
			return false;
		}

		return move.Length == 4 || move[4] is 'q' or 'r' or 'b' or 'n';
	}

	private static bool IsFile(char value) => value is >= 'a' and <= 'h';
	private static bool IsRank(char value) => value is >= '1' and <= '8';
}
