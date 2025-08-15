using Bezoro.Core.Common.Extensions;
using Bezoro.UCI.API.Types;

namespace Bezoro.UCI.API;

public readonly record struct Move()
{
	public Move(
		string       notation,
		MoveAnalysis analysis) : this()
	{
		var parsedMove = new ParsedMove(notation);
		From     = parsedMove.From;
		To       = parsedMove.To;
		Notation = parsedMove.Notation;
		Analysis = analysis;
		Piece    = parsedMove.Piece ?? default;
	}

	public MoveAnalysis Analysis { get; }

	public Piece       Piece      { get; }
	public PlayerColor MovingSide => Piece.Color;
	public string      From       { get; }
	public string      Notation   { get; }

	public string To { get; }
}

public readonly record struct MoveScore()
{
	public MoveScore(int? scoreCp, int? scoreMate) : this()
	{
		ScoreCp   = scoreCp;
		ScoreMate = scoreMate;
	}

	public int? ScoreCp   { get; }
	public int? ScoreMate { get; }

	public static bool TryParse(string line, out MoveScore? score)
	{
		line.ThrowIfNull();

		int? scoreCp   = null;
		int? scoreMate = null;
		score = null;

		int scoreIdx = line.IndexOf(" score ", StringComparison.OrdinalIgnoreCase);
		if (scoreIdx < 0)
		{
			score = null;
			return false;
		}

		int mateIdx = line.IndexOf(" mate ", scoreIdx, StringComparison.OrdinalIgnoreCase);
		if (mateIdx >= 0)
		{
			int start        = mateIdx + 6;
			int end          = line.IndexOf(' ', start);
			if (end < 0) end = line.Length;

			if (!int.TryParse(line.AsSpan(start, end - start), out int mateScore))
			{
				score = null;
				return false;
			}

			scoreMate = mateScore;
		}

		int cpIdx = line.IndexOf(" cp ", scoreIdx, StringComparison.Ordinal);
		if (cpIdx >= 0)
		{
			int start        = cpIdx + 4;
			int end          = line.IndexOf(' ', start);
			if (end < 0) end = line.Length;

			if (!int.TryParse(line.AsSpan(start, end - start), out int cpScore))
			{
				score = null;
				return false;
			}

			scoreCp = cpScore;
		}

		if (scoreCp == null && scoreMate == null)
		{
			score = null;
			return false;
		}

		score = new(scoreCp, scoreMate);
		return true;
	}
}

public readonly record struct MoveToPieceMap(string Move, char Piece)
{
	public static MoveToPieceMap Map(Fen fen, string move)
	{
		if (string.IsNullOrWhiteSpace(move) || move.Length < 4)
			return new(move, '\0');

		string from = move[..2];

		var board = new BoardState(fen);
		if (board.TryGetPieceAt(from, out var piece) && piece.HasValue)
			return new(move, piece.Value.Char);

		return new(move, '\0');
	}
}

public readonly record struct ParsedMove()
{
	public ParsedMove(string moveNotation) : this()
	{
		moveNotation.ThrowIfNull();
		moveNotation.Length.ThrowIfLessThan(4);

		Piece = new Piece(moveNotation[0]);
		if (!Piece.Value.Char.IsNull())
			moveNotation = moveNotation[1..];

		Notation = moveNotation.ToLowerInvariant();
		From     = Notation[..2];
		To       = Notation[2..4];
	}

	public Piece? Piece { get; }
	public string From  { get; }

	public string Notation { get; }
	public string To       { get; }
}
