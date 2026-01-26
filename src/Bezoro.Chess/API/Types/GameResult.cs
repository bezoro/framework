namespace Bezoro.Chess.API.Types;

/// <summary>
///     Represents the outcome of a chess game.
/// </summary>
public enum GameOutcome
{
	/// <summary>The game is still in progress.</summary>
	Ongoing,

	/// <summary>White wins the game.</summary>
	WhiteWins,

	/// <summary>Black wins the game.</summary>
	BlackWins,

	/// <summary>The game is a draw.</summary>
	Draw
}

/// <summary>
///     Represents the reason a game ended.
/// </summary>
public enum TerminationReason
{
	/// <summary>Game is still ongoing.</summary>
	None,

	/// <summary>The losing side was checkmated.</summary>
	Checkmate,

	/// <summary>No legal moves available but not in check.</summary>
	Stalemate,

	/// <summary>A player ran out of time.</summary>
	Timeout,

	/// <summary>A player resigned.</summary>
	Resignation,

	/// <summary>Players agreed to a draw.</summary>
	Agreement,

	/// <summary>Threefold repetition of position.</summary>
	ThreefoldRepetition,

	/// <summary>Fifty moves without capture or pawn move.</summary>
	FiftyMoveRule,

	/// <summary>Insufficient material to checkmate.</summary>
	InsufficientMaterial,

	/// <summary>One side claimed a draw.</summary>
	DrawClaimed
}

/// <summary>
///     Represents the complete result of a chess game including outcome and termination reason.
/// </summary>
/// <param name="Outcome">The game outcome (ongoing, white wins, black wins, draw).</param>
/// <param name="Reason">The reason the game ended.</param>
public readonly record struct GameResult(GameOutcome Outcome, TerminationReason Reason)
{
	/// <summary>
	///     Creates a result for black winning by checkmate.
	/// </summary>
	public static GameResult BlackCheckmates { get; } = new(GameOutcome.BlackWins, TerminationReason.Checkmate);

	/// <summary>
	///     Creates a draw result by agreement.
	/// </summary>
	public static GameResult DrawByAgreement { get; } = new(GameOutcome.Draw, TerminationReason.Agreement);

	/// <summary>
	///     Creates a draw result by fifty move rule.
	/// </summary>
	public static GameResult FiftyMoveRule { get; } = new(GameOutcome.Draw, TerminationReason.FiftyMoveRule);

	/// <summary>
	///     Creates a draw result by insufficient material.
	/// </summary>
	public static GameResult InsufficientMaterial { get; } =
		new(GameOutcome.Draw, TerminationReason.InsufficientMaterial);

	/// <summary>
	///     Gets a result representing an ongoing game.
	/// </summary>
	public static GameResult Ongoing { get; } = new(GameOutcome.Ongoing, TerminationReason.None);

	/// <summary>
	///     Creates a result for a stalemate draw.
	/// </summary>
	public static GameResult Stalemate { get; } = new(GameOutcome.Draw, TerminationReason.Stalemate);

	/// <summary>
	///     Creates a draw result by threefold repetition.
	/// </summary>
	public static GameResult ThreefoldRepetition { get; } =
		new(GameOutcome.Draw, TerminationReason.ThreefoldRepetition);

	/// <summary>
	///     Creates a result for white winning by checkmate.
	/// </summary>
	public static GameResult WhiteCheckmates { get; } = new(GameOutcome.WhiteWins, TerminationReason.Checkmate);

	/// <summary>
	///     Gets a value indicating whether the result is a draw.
	/// </summary>
	public bool IsDraw => Outcome == GameOutcome.Draw;

	/// <summary>
	///     Gets a value indicating whether the game has ended.
	/// </summary>
	public bool IsGameOver => Outcome != GameOutcome.Ongoing;

	/// <summary>
	///     Gets a value indicating whether the game is still in progress.
	/// </summary>
	public bool IsOngoing => Outcome == GameOutcome.Ongoing;

	/// <summary>
	///     Gets the winning player color, or null if the game is ongoing or a draw.
	/// </summary>
	public PlayerColor? Winner => Outcome switch
	{
		GameOutcome.WhiteWins => PlayerColor.White,
		GameOutcome.BlackWins => PlayerColor.Black,
		_                     => null
	};

	/// <summary>
	///     Creates a checkmate result for the specified winner.
	/// </summary>
	public static GameResult Checkmate(PlayerColor winner) =>
		new(winner == PlayerColor.White ? GameOutcome.WhiteWins : GameOutcome.BlackWins, TerminationReason.Checkmate);

	/// <summary>
	///     Creates a resignation result for the specified winner.
	/// </summary>
	public static GameResult Resignation(PlayerColor winner) =>
		new(winner == PlayerColor.White ? GameOutcome.WhiteWins : GameOutcome.BlackWins, TerminationReason.Resignation);

	/// <summary>
	///     Creates a timeout result for the specified winner.
	/// </summary>
	public static GameResult Timeout(PlayerColor winner) =>
		new(winner == PlayerColor.White ? GameOutcome.WhiteWins : GameOutcome.BlackWins, TerminationReason.Timeout);

	/// <summary>
	///     Returns the PGN result string (1-0, 0-1, 1/2-1/2, or *).
	/// </summary>
	public string ToPgnResult() => Outcome switch
	{
		GameOutcome.WhiteWins => "1-0",
		GameOutcome.BlackWins => "0-1",
		GameOutcome.Draw      => "1/2-1/2",
		_                     => "*"
	};
}
