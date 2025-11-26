using System;
using System.Text;
using Bezoro.Chess.API;
using Bezoro.Chess.API.Types;

namespace Bezoro.Chess.Internal;

/// <summary>
///     Exports chess games to PGN (Portable Game Notation) format.
/// </summary>
internal static class PgnExporter
{
    /// <summary>
    ///     Exports a chess game to PGN format.
    /// </summary>
    public static string Export(ChessGame game)
	{
		var sb = new StringBuilder();

		// PGN Headers
		AppendHeader(sb, "Event",  "Casual Game");
		AppendHeader(sb, "Site",   "Bezoro Chess");
		AppendHeader(sb, "Date",   DateTime.Now.ToString("yyyy.MM.dd"));
		AppendHeader(sb, "Round",  "?");
		AppendHeader(sb, "White",  game.Options.PlayerColor == PlayerColor.White ? "Player" : "Engine");
		AppendHeader(sb, "Black",  game.Options.PlayerColor == PlayerColor.Black ? "Player" : "Engine");
		AppendHeader(sb, "Result", game.Result.ToPgnResult());

		// Time control if not unlimited
		if (!game.Clock.IsUnlimited)
		{
			var initial   = (int)(game.Options.EffectiveTimeControl.WhiteTimeMs / 1000);
			var increment = (int)(game.Options.EffectiveTimeControl.IncrementMs / 1000);
			AppendHeader(sb, "TimeControl", $"{initial}+{increment}");
		}

		sb.AppendLine();

		// Move text
		var moves      = game.History;
		var moveNumber = 1;
		var lineLength = 0;

		for (var i = 0; i < moves.Count; i++)
		{
			var move     = moves[i];
			var moveText = new StringBuilder();

			// Move number for white
			if (move.MovingColor == PlayerColor.White)
				moveText.Append($"{moveNumber}. ");
			else if (i == 0)
				// Black moved first (from custom position)
				moveText.Append($"{moveNumber}... ");

			// Move notation (using UCI for simplicity; ideally would convert to SAN)
			moveText.Append(move.SanNotation ?? move.Notation);

			// Check/checkmate suffix
			if (move.IsCheckmate)
				moveText.Append('#');
			else if (move.IsCheck)
				moveText.Append('+');

			moveText.Append(' ');

			// Wrap lines at 80 characters
			if (lineLength + moveText.Length > 80)
			{
				sb.AppendLine();
				lineLength = 0;
			}

			sb.Append(moveText);
			lineLength += moveText.Length;

			// Increment move number after black moves
			if (move.MovingColor == PlayerColor.Black)
				moveNumber++;
		}

		// Append result
		if (lineLength > 0 && lineLength + game.Result.ToPgnResult().Length > 80)
			sb.AppendLine();

		sb.Append(game.Result.ToPgnResult());
		sb.AppendLine();

		return sb.ToString();
	}

    /// <summary>
    ///     Exports just the move list (without headers) in coordinate notation.
    /// </summary>
    public static string ExportMoveList(ChessGame game)
	{
		var sb         = new StringBuilder();
		var moves      = game.History;
		var moveNumber = 1;

		for (var i = 0; i < moves.Count; i++)
		{
			var move = moves[i];

			if (move.MovingColor == PlayerColor.White)
			{
				if (i > 0) sb.Append(' ');
				sb.Append($"{moveNumber}.");
			}

			sb.Append(' ');
			sb.Append(move.Notation);

			if (move.MovingColor == PlayerColor.Black)
				moveNumber++;
		}

		return sb.ToString().Trim();
	}

	private static string EscapePgnString(string value) =>
		value
			.Replace("\\", "\\\\")
			.Replace("\"", "\\\"");

	private static void AppendHeader(StringBuilder sb, string name, string value)
	{
		sb.Append('[');
		sb.Append(name);
		sb.Append(" \"");
		sb.Append(EscapePgnString(value));
		sb.Append("\"]");
		sb.AppendLine();
	}
}
