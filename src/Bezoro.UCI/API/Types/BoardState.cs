using System.Collections.Generic;
using Bezoro.Core.Common.Extensions;
using Bezoro.UCI.API.Common.Enums;

namespace Bezoro.UCI.API.Types;

public readonly record struct BoardState()
{
	private BoardState(Fen fen, IReadOnlyCollection<Position> positions) : this()
	{
		Fen       = fen;
		Positions = positions;
	}

	public Fen                           Fen       { get; }
	public IReadOnlyCollection<Position> Positions { get; }

	public PieceColor ActiveColor => Fen.ActiveColor switch
	{
		'w' => PieceColor.White,
		'b' => PieceColor.Black
	};

	public static BoardState? FromFen(Fen fen)
	{
		if (!Fen.Validate(fen.Raw)) return null;

		return new(fen, BuildPositionsFromFen(fen));
	}

	public bool TryGetPieceAt(string squareNotation, out Piece? piece)
	{
		piece = null;
		if (squareNotation.IsNullOrEmpty()) return false;

		string normalizedSquare = squareNotation.Trim().ToLowerInvariant();
		if (normalizedSquare.Length < 2) return false;
		if (!IsValidSquare(normalizedSquare)) return false;

		foreach (var pos in Positions)
		{
			if (pos.Notation != normalizedSquare) continue;

			piece = pos.Piece;
			return true;
		}

		return false;
	}

	private static bool IsValidSquare(string sq)
	{
		if (sq.Length < 2) return false;

		char file = sq[0];
		char rank = sq[1];
		return file is >= 'a' and <= 'h' && rank is >= '1' and <= '8';
	}

	private static List<Position> BuildPositionsFromFen(Fen fen)
	{
		var    positions = new List<Position>(64);
		string placement = fen.PiecePlacement;

		static string BuildSquare(int fileIndex, int rankIndex) => $"{(char)('a' + fileIndex)}{rankIndex}";

		var rank = 8;
		var file = 0;

		foreach (char token in placement)
		{
			switch (token)
			{
				case '/':
				{
					// Fill remaining squares in the rank if needed
					while (file < 8)
					{
						positions.Add(Position.Create(BuildSquare(file, rank), null));
						file++;
					}

					rank--;
					file = 0;
					continue;
				}
				case >= '1' and <= '8':
				{
					int empties = token - '0';
					for (var i = 0; i < empties && file < 8; i++)
					{
						positions.Add(Position.Create(BuildSquare(file, rank), null));
						file++;
					}

					continue;
				}
			}

			if (!char.IsLetter(token)) continue;

			var piece = Piece.FromChar(token);

			if (file >= 8) continue;

			positions.Add(Position.Create(BuildSquare(file, rank), piece));
			file++;
		}

		// Fill any remaining squares if placement didn't cover all 64
		while (rank >= 1)
		{
			while (file < 8)
			{
				positions.Add(Position.Create(BuildSquare(file, rank), null));
				file++;
			}

			rank--;
			file = 0;
		}

		return positions;
	}
}
