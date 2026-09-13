namespace Bezoro.Chess.UCI.API.Types;

/// <summary>
///     Represents a board square and its optional occupying piece.
/// </summary>
public readonly record struct Position()
{
	private Position(string notation, Piece? piece) : this()
	{
		Notation = notation;
		Piece    = piece;
	}

	/// <summary>
	///     Creates a board position value.
	/// </summary>
	/// <param name="notation">Algebraic square notation such as <c>e4</c>.</param>
	/// <param name="piece">The occupying piece, or <see langword="null" /> for an empty square.</param>
	/// <returns>The position value.</returns>
	public static Position Create(string notation, Piece? piece) => new(notation, piece);

	/// <summary>Gets the occupying piece, when present.</summary>
	public Piece? Piece { get; }

	/// <summary>Gets the algebraic square notation.</summary>
	public string Notation { get; } = string.Empty;
}
