using Bezoro.Core.Extensions;
using Bezoro.Chess.UCI.API.Common.Enums;
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;

namespace Bezoro.Chess.UCI.API.Types;

/// <summary>
///     Represents a legal chess move and its specific characteristics,
///     such as whether it's a capture, castling, or promotion, etc...
/// </summary>
public readonly record struct MoveAnalysis
{
	public bool      IsCapture   { get; private init; }
	public bool      IsCastling  { get; private init; }
	public bool      IsCheck     { get; private init; }
	public bool      IsEnPassant { get; private init; }
	public bool      IsMate      { get; private init; }
	public bool      IsNormal    { get; private init; }
	public bool      IsPromotion { get; private init; }
	public bool      IsStalemate { get; private init; }
	public MoveScore Score       { get; private init; }
	public Piece?    MovingPiece { get; private init; }


	/// <summary>
	///     Synchronous analyzer used when an engine-derived score is available.
	///     Delegates structural and tactical move semantics to the protocol-layer FEN classifier,
	///     then overlays the supplied engine score onto the result.
	/// </summary>
	internal static MoveAnalysis Analyze(string moveNotation, BoardState boardState, MoveScore score, bool isStalemate)
	{
		moveNotation.ThrowIfNull();
		boardState.ThrowIfNull();

		var parsedMove = ParsedMove.FromNotation(moveNotation);

		if (!boardState.TryGetPieceAt(parsedMove.From, out var movingPiece) || movingPiece is null)
			throw new ArgumentException(
				$"No piece found on square '{parsedMove.From}' for move '{moveNotation}'.",
				nameof(boardState)
			);

		var classification = boardState.Fen.ClassifyMoveFully(parsedMove.Raw.ToLowerInvariant());
		bool isMate = classification.IsMate || score.ScoreMate == -1;
		bool isCheck = classification.IsCheck || isMate;

		return new()
		{
			IsCapture   = classification.IsCapture,
			IsCastling  = classification.IsCastling,
			IsCheck     = isCheck,
			IsEnPassant = classification.IsEnPassant,
			IsMate      = isMate,
			IsNormal    = classification.IsNormal,
			IsPromotion = classification.IsPromotion,
			IsStalemate = classification.IsStalemate || isStalemate,
			Score       = score,
			MovingPiece = movingPiece
		};
	}
}
