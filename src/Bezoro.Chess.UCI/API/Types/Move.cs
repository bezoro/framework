using Bezoro.Chess.UCI.API.Common.Enums;

namespace Bezoro.Chess.UCI.API.Types;

/// <summary>
///     Represents a parsed chess move together with its analyzed board semantics.
/// </summary>
public readonly record struct Move()
{
	/// <summary>
	///     Initializes a move from notation and a corresponding analysis result.
	/// </summary>
	/// <param name="notation">Coordinate move notation, optionally including a piece or promotion designator.</param>
	/// <param name="analysis">The analyzed move semantics.</param>
	/// <exception cref="InvalidOperationException">The moving piece cannot be resolved.</exception>
	public Move(string notation, MoveAnalysis analysis) : this()
	{
		var parsedMove = ParsedMove.FromNotation(notation);
		From     = parsedMove.From;
		To       = parsedMove.To;
		Notation = parsedMove.Notation;
		Analysis = analysis;
		Piece    = ResolvePiece(parsedMove, analysis, notation);
	}

	/// <summary>Gets the analyzed move semantics.</summary>
	public MoveAnalysis Analysis { get; }

	/// <summary>Gets the piece that moves.</summary>
	public Piece Piece { get; }

	/// <summary>Gets the side that owns <see cref="Piece" />.</summary>
	public PieceColor   MovingSide => Piece.Color;

	/// <summary>Gets the source square.</summary>
	public string From { get; } = string.Empty;

	/// <summary>Gets normalized coordinate notation.</summary>
	public string Notation { get; } = string.Empty;

	/// <summary>Gets the destination square.</summary>
	public string To { get; } = string.Empty;

	private static Piece ResolvePiece(ParsedMove parsedMove, MoveAnalysis analysis, string notation)
	{
		var resolved = analysis.MovingPiece;
		if (resolved is null && parsedMove.MovingPiece.Char != '\0')
			resolved = parsedMove.MovingPiece;

		if (resolved is null)
			throw new InvalidOperationException(
				$"Unable to determine moving piece for move '{notation}'. Ensure the move was analyzed with a valid board state or include a piece designator."
			);

		return resolved.Value;
	}
}
