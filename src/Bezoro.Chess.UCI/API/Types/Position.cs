namespace Bezoro.Chess.UCI.API.Types;

public readonly record struct Position()
{
	private Position(string notation, Piece? piece) : this()
	{
		Notation = notation;
		Piece    = piece;
	}

	public static Position Create(string notation, Piece? piece) => new(notation, piece);

	public Piece? Piece    { get; }
	public string Notation { get; }
}
