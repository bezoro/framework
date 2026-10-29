namespace Bezoro.Chess.UCI.Protocol.API.Abstractions;

/// <summary>Supplies optional book, brilliant, great, or missed-opportunity annotations using application-owned rules.</summary>
public interface IMoveReviewAnnotationPolicy
{
	/// <summary>Inspects a completed move review and returns a special annotation.</summary>
	/// <param name="move">Move review with scores, classifications, and both principal variations.</param>
	/// <param name="playerRating">Optional rating of the player who moved.</param>
	/// <returns>An application-defined annotation, or <see cref="MoveReviewAnnotation.None" />.</returns>
	MoveReviewAnnotation Annotate(ReviewedMove move, int? playerRating);
}
