using Bezoro.UCI.API.Common.Enums;

namespace Bezoro.UCI.API.Types;

public readonly record struct Move()
{
	public Move(string notation, MoveAnalysis analysis) : this()
	{
		var parsedMove = ParsedMove.FromNotation(notation);
		From     = parsedMove.From;
		To       = parsedMove.To;
		Notation = parsedMove.Notation;
		Analysis = analysis;
		Piece    = parsedMove.MovingPiece;
	}

	public MoveAnalysis Analysis   { get; }
	public Piece        Piece      { get; }
	public PieceColor   MovingSide => Piece.Color;
	public string       From       { get; }
	public string       Notation   { get; }
	public string       To         { get; }
}
