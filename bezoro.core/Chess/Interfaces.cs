namespace Bezoro.Core.Chess
{
	public interface IChessBoardModel
	{
		IChessBoardSquareModel[,] Squares { get; }
		IChessPieceModel?[]       Pieces  { get; }
		int                       Height  { get; }
		int                       Width   { get; }
	}

	public interface IChessBoardSquareModel
	{
		bool              IsEmpty                  { get; }
		bool              IsHighlightedAsValidMove { get; set; }
		bool              IsOccupied               { get; }
		bool              IsSelected               { get; set; }
		ChessPosition     Position                 { get; }
		IChessPieceModel? Piece                    { get; set; }
	}

	public interface IChessCommand { }

	public interface IChessPieceModel
	{
		bool                   IsCaptured { get; set; }
		bool                   IsSelected { get; set; }
		ChessPieceType         Type       { get; }
		ChessPosition          Position   { get; set; }
		IChessBoardSquareModel Square     { get; set; }
		PlayerColor            Color      { get; }
	}
}
