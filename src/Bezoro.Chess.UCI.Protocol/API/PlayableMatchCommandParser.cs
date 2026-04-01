using System.Collections.Immutable;

namespace Bezoro.Chess.UCI.Protocol.API;

/// <summary>
///     Parses simple textual commands for playable-match workflows such as UCI moves, history, undo, and FEN loading.
/// </summary>
public static class PlayableMatchCommandParser
{
	/// <summary>
	///     Parses a single user input line into a playable-match command.
	/// </summary>
	/// <param name="input">Raw input line.</param>
	/// <returns>The parsed command result.</returns>
	public static PlayableMatchCommand Parse(string input)
	{
		string normalized = (input ?? string.Empty).Trim();
		if (string.IsNullOrWhiteSpace(normalized))
			return Invalid("Enter a move in UCI notation such as e2e4 or a7a8q.");

		string lower = normalized.ToLowerInvariant();
		if (lower is "quit" or "exit")
			return new(PlayableMatchCommandKind.Quit);

		if (lower == "moves")
			return new(PlayableMatchCommandKind.Moves);

		if (lower == "history")
			return new(PlayableMatchCommandKind.History);

		if (lower == "undo")
			return new(PlayableMatchCommandKind.Undo);

		if (lower.StartsWith("loadfen ", StringComparison.Ordinal))
			return ParseLoadFen(normalized["loadfen ".Length..]);

		if (!UciEngineClient.IsUciMoveString(lower))
			return Invalid("Enter a move in UCI notation such as e2e4 or a7a8q, or use loadfen <fen>.");

		return new(PlayableMatchCommandKind.Move, lower);
	}

	private static PlayableMatchCommand Invalid(string error) =>
		new(PlayableMatchCommandKind.Invalid, Error: error);

	private static PlayableMatchCommand ParseLoadFen(string payload)
	{
		string trimmed = payload.Trim();
		if (string.IsNullOrWhiteSpace(trimmed))
			return Invalid("loadfen requires a FEN after the command name.");

		int     movesTokenIndex = trimmed.IndexOf(" moves ", StringComparison.OrdinalIgnoreCase);
		string  fenText         = movesTokenIndex >= 0 ? trimmed[..movesTokenIndex].Trim() : trimmed;
		string? movesText       = movesTokenIndex >= 0 ? trimmed[(movesTokenIndex + 7)..].Trim() : null;

		var parsedFen = Fen.Parse(fenText);
		if (!parsedFen.HasValue)
			return Invalid("loadfen requires a valid FEN.");

		if (string.IsNullOrWhiteSpace(movesText))
			return new(PlayableMatchCommandKind.LoadFen, Fen: parsedFen.Value, Moves: []);

		string[] rawMoves = movesText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		var      builder  = ImmutableArray.CreateBuilder<string>(rawMoves.Length);
		foreach (string move in rawMoves)
		{
			string normalizedMove = move.Trim().ToLowerInvariant();
			if (!UciEngineClient.IsUciMoveString(normalizedMove))
				return Invalid("loadfen moves must be valid UCI notation such as e2e4.");

			builder.Add(normalizedMove);
		}

		return new(PlayableMatchCommandKind.LoadFen, Fen: parsedFen.Value, Moves: builder.ToImmutable());
	}
}
