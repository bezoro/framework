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
		Piece    = ResolvePiece(parsedMove, analysis, notation);
	}

	public MoveAnalysis Analysis   { get; }
	public Piece        Piece      { get; }
	public PieceColor   MovingSide => Piece.Color;
	public string       From       { get; }
	public string       Notation   { get; }
	public string       To         { get; }

	private static Piece ResolvePiece(ParsedMove parsedMove, MoveAnalysis analysis, string notation)
	{
		var resolved = analysis.MovingPiece;
		if (resolved is null && parsedMove.MovingPiece.Char != '\0')
			resolved = parsedMove.MovingPiece;

		if (resolved is null)
			throw new InvalidOperationException(
				$"Unable to determine moving piece for move '{notation}'. Ensure the move was analyzed with a valid board state or include a piece designator.");

		return resolved.Value;
	}
}
