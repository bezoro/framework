namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>Classifies a played move by the expected points it loses.</summary>
public enum MoveQuality
{
	/// <summary>No measured expected points loss, including the engine's preferred move.</summary>
	Best,
	/// <summary>Positive loss smaller than 0.02.</summary>
	Excellent,
	/// <summary>Loss at least 0.02 and smaller than 0.05.</summary>
	Good,
	/// <summary>Loss at least 0.05 and smaller than 0.10.</summary>
	Inaccuracy,
	/// <summary>Loss at least 0.10 and smaller than 0.20.</summary>
	Mistake,
	/// <summary>Loss at least 0.20.</summary>
	Blunder
}

