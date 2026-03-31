using System.Collections.Generic;

namespace Bezoro.Chess.UCI.Protocol.API.Common.Extensions;

/// <summary>
///     Extension methods for rendering <see cref="PositionAdvantage" /> values.
/// </summary>
public static class PositionAdvantageExtensions
{
	/// <summary>
	///     Builds a simple text bar for the supplied advantage value.
	/// </summary>
	/// <param name="advantage">Advantage value to render.</param>
	/// <param name="height">Bar height in rows.</param>
	/// <param name="width">Bar width in characters.</param>
	/// <returns>Display lines suitable for console output.</returns>
	public static string[] ToDisplayBarLines(this PositionAdvantage advantage, int height = 8, int width = 3)
	{
		var lines = new List<string>(height + 5)
		{
			"Advantage",
			"  Engine",
			"  +" + new string('-', width) + "+"
		};

		int playerRows = (int)Math.Round((advantage.Normalized + 1.0) / 2.0 * height);
		playerRows = Math.Clamp(playerRows, 0, height);
		int engineRows = height - playerRows;

		for (var row = 0; row < height; row++)
		{
			string fill = row < engineRows
				? new string('=', width)
				: new string('#', width);

			lines.Add($"  |{fill}|");
		}

		lines.Add("  +" + new string('-', width) + "+");
		lines.Add("   You");
		lines.Add($"  {advantage.Summary}");

		return lines.ToArray();
	}
}
