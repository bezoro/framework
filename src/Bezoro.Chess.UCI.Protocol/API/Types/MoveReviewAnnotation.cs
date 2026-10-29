namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>Optional annotations supplied by an application's opening book or review policy.</summary>
public enum MoveReviewAnnotation
{
	/// <summary>No special annotation.</summary>
	None,
	/// <summary>An opening-book move recognized by the application.</summary>
	Book,
	/// <summary>A brilliant move recognized by the application's policy.</summary>
	Brilliant,
	/// <summary>A great move recognized by the application's policy.</summary>
	Great,
	/// <summary>A missed opportunity recognized by the application's policy.</summary>
	Miss
}
