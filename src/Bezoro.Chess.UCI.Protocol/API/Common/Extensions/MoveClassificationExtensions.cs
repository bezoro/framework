using System.Collections.Immutable;

namespace Bezoro.Chess.UCI.Protocol.API.Common.Extensions;

/// <summary>
///     Extension methods for compact debug display of move classifications.
/// </summary>
public static class MoveClassificationExtensions
{
	/// <summary>
	///     Returns compact debug tags such as <c>capture</c>, <c>promotion=q</c>, or <c>mate</c>.
	/// </summary>
	/// <param name="classification">Classification to format.</param>
	/// <returns>Ordered tags for the classification.</returns>
	public static ImmutableArray<string> ToDebugTags(this MoveClassification classification)
	{
		if (classification.MovingPiece == '\0')
			return [];

		var builder = ImmutableArray.CreateBuilder<string>(8);
		if (classification.IsNormal)
			builder.Add("normal");

		if (classification.IsCapture)
			builder.Add("capture");

		if (classification.IsEnPassant)
			builder.Add("en-passant");

		if (classification.IsPromotion)
			builder.Add($"promotion={classification.PromotionPiece}");

		if (classification.IsKingsideCastling)
			builder.Add("castle-kingside");

		if (classification.IsQueensideCastling)
			builder.Add("castle-queenside");

		if (classification.IsDoublePawnPush)
			builder.Add("double-pawn-push");

		if (classification.IsCheck)
			builder.Add("check");

		if (classification.IsMate)
			builder.Add("mate");

		if (classification.IsStalemate)
			builder.Add("stalemate");

		return builder.ToImmutable();
	}

	/// <summary>
	///     Returns a bracketed debug suffix such as <c> [capture,promotion=q]</c>, or an empty string when absent.
	/// </summary>
	/// <param name="classification">Classification to format.</param>
	/// <returns>Bracketed debug suffix or an empty string.</returns>
	public static string ToDebugSuffix(this MoveClassification classification)
	{
		var tags = classification.ToDebugTags();
		return tags.IsDefaultOrEmpty ? string.Empty : $" [{string.Join(",", tags)}]";
	}
}
