using Bezoro.Core.Types;
using Bezoro.UCI.API.Types;

namespace Bezoro.Chess.Internal;

/// <summary>
///     Builds a 2D grid board representation from a FEN string for Unity rendering.
/// </summary>
internal static class BoardViewBuilder
{
	/// <summary>
	///     Builds an 8x8 board grid from a FEN position.
	///     Grid is indexed [file, rank] where file 0 = 'a' and rank 0 = '1'.
	///     Uppercase = white pieces, lowercase = black pieces, null = empty square.
	/// </summary>
	public static Grid2D<char?> Build(Fen fen)
	{
		var    board     = new Grid2D<char?>(8, 8);
		string placement = fen.PiecePlacement;

		var rank = 7; // Start at rank 8 (index 7)
		var file = 0;

		foreach (char token in placement)
		{
			switch (token)
			{
				case '/':
					rank--;
					file = 0;
					continue;

				case >= '1' and <= '8':
					// Empty squares - already null by default, just advance file
					file += token - '0';
					continue;
			}

			if (char.IsLetter(token) && file < 8 && rank >= 0)
			{
				board[file, rank] = token;
				file++;
			}
		}

		return board;
	}

	/// <summary>
	///     Converts file index (0-7) to letter ('a'-'h').
	/// </summary>
	public static char FileToLetter(int file) => (char)('a' + file);

	/// <summary>
	///     Converts rank index (0-7) to number character ('1'-'8').
	/// </summary>
	public static char RankToNumber(int rank) => (char)('1' + rank);

	/// <summary>
	///     Gets the piece at a specific square notation (e.g., "e4").
	/// </summary>
	public static char? GetPieceAt(Grid2D<char?> board, string square)
	{
		if (square.Length < 2)
			return null;

		int file = square[0] - 'a';
		int rank = square[1] - '1';

		return board.TryGet(file, rank, out char? piece) ? piece : null;
	}

	/// <summary>
	///     Gets the square notation from file and rank indices.
	/// </summary>
	public static string GetSquare(int file, int rank) => $"{FileToLetter(file)}{RankToNumber(rank)}";
}
