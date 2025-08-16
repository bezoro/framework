using System.Collections.Generic;
using Bezoro.Core.Common.Extensions;
using Bezoro.UCI.API.Common.Enums;

namespace Bezoro.UCI.API.Types;

public readonly record struct BoardState()
{
	public BoardState(Fen fen) : this()
	{
		Fen       = fen;
		Positions = BuildPositionsFromFen(fen);
	}

	public Fen                           Fen       { get; }
	public IReadOnlyCollection<Position> Positions { get; }

	public PieceColor ActiveColor => Fen.ActiveColor == 'w' ? PieceColor.White : PieceColor.Black;

	public bool TryGetPieceAt(string positionNotation, out Piece? piece)
	{
		positionNotation.ThrowIfNull();

		string sq = positionNotation.Trim().ToLowerInvariant();
		sq.Length.ThrowIfLessThan(2);

		piece = null;
		char file = sq[0];
		char rank = sq[1];

		if (file < 'a' || file > 'h' || rank < '1' || rank > '8') return false;

		foreach (var pos in Positions)
		{
			if (pos.Notation != sq) continue;

			piece = pos.Piece;
			return true;
		}

		return false;
	}

	private static List<Position> BuildPositionsFromFen(Fen fen)
	{
		var    positions = new List<Position>(64);
		string placement = fen.PiecePlacement;

		var rank = 8;
		var file = 0;

		foreach (char token in placement)
		{
			if (token == '/')
			{
				// Fill remaining squares in the rank if needed
				while (file < 8)
				{
					var sq = $"{(char)('a' + file)}{rank}";
					positions.Add(Position.Create(sq, null));
					file++;
				}

				rank--;
				file = 0;
				continue;
			}

			if (token is >= '1' and <= '8')
			{
				int empties = token - '0';
				for (var i = 0; i < empties && file < 8; i++)
				{
					var sq = $"{(char)('a' + file)}{rank}";
					positions.Add(Position.Create(sq, null));
					file++;
				}

				continue;
			}

			if (!char.IsLetter(token)) continue;

			{
				var piece = Piece.FromChar(token);

				if (file >= 8) continue;

				var sq = $"{(char)('a' + file)}{rank}";
				positions.Add(Position.Create(sq, piece));
				file++;
			}
		}

		// Fill any remaining squares if placement didn't cover all 64
		while (rank >= 1)
		{
			while (file < 8)
			{
				var sq = $"{(char)('a' + file)}{rank}";
				positions.Add(Position.Create(sq, null));
				file++;
			}

			rank--;
			file = 0;
		}

		return positions;
	}
}
