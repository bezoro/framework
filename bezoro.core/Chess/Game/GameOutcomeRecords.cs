using Bezoro.Core.Chess.Common.Enums;
using JetBrains.Annotations;

namespace Bezoro.Core.Chess.Game
{
	/// <summary>
	///     Interface all specific game outcomes.
	///     A game state will hold an instance of a GameOutcome-derived type
	///     when <see cref="GameStatus" /> is <see cref="GameStatus.Finished" />.
	/// </summary>
	public interface IGameOutcome { }

	/// <summary>
	///     Represents a game drawn by mutual agreement between the players.
	/// </summary>
	public sealed record AgreementDraw : DrawOutcome;

	/// <summary>
	///     Represents a game ended by checkmate.
	/// </summary>
	/// <param name="Winner">The <see cref="PlayerColor" /> of the player who delivered checkmate.</param>
	public sealed record Checkmate(PlayerColor Winner) : WinOutcome(Winner);

	/// <summary>
	///     Base class for outcomes that are a draw.
	/// </summary>
	public abstract record DrawOutcome : IGameOutcome;

	/// <summary>
	///     Represents a game drawn due to the fifty-move rule.
	///     (50 consecutive moves by each side with no pawn move and no capture).
	/// </summary>
	public sealed record FiftyMoveRuleDraw : DrawOutcome;

	/// <summary>
	///     Represents a game drawn due to insufficient mating material on the board for either side to force a checkmate.
	///     (e.g., King vs. King, King + Bishop vs. King). This outcome is determined by the board state itself,
	///     distinct from a draw caused by time forfeit where the opponent lacks material.
	/// </summary>
	public sealed record InsufficientMaterialOnBoardDraw : DrawOutcome;

	/// <summary>
	///     Represents a game drawn due to repetition of the position.
	///     (Typically, the same position appearing three times with the same side to move and identical castling/en passant
	///     rights).
	/// </summary>
	public sealed record RepetitionDraw : DrawOutcome;

	/// <summary>
	///     Represents a game ended by resignation.
	/// </summary>
	/// <param name="WinningPlayer">The <see cref="PlayerColor" /> of the player who won due to the opponent's resignation.</param>
	/// <param name="ResigningPlayer">The <see cref="PlayerColor" /> of the player who resigned.</param>
	public sealed record Resignation(PlayerColor WinningPlayer, [UsedImplicitly] PlayerColor ResigningPlayer)
		: WinOutcome(WinningPlayer)
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="Resignation" /> class,
		///     automatically determining the winner based on the resigning player.
		/// </summary>
		/// <param name="resigningPlayer">The player who resigned.</param>
		public Resignation(PlayerColor resigningPlayer)
			: this(resigningPlayer == PlayerColor.White ? PlayerColor.Black : PlayerColor.White, resigningPlayer) { }
	}

	/// <summary>
	///     Represents a game drawn due to stalemate.
	///     The player whose turn it was had no legal moves and was not in check.
	/// </summary>
	public sealed record StalemateDraw : DrawOutcome;

	/// <summary>
	///     Represents a game drawn due to a player's clock running out,
	///     but their opponent lacked sufficient material to deliver mate.
	/// </summary>
	/// <param name="TimedOutPlayer">The <see cref="PlayerColor" /> of the player whose clock ran out.</param>
	public sealed record TimeForfeitInsufficientMaterialDraw([UsedImplicitly] PlayerColor TimedOutPlayer) : DrawOutcome;

	/// <summary>
	///     Represents a game ended by time forfeit where the opponent had sufficient mating material.
	/// </summary>
	/// <param name="WinningPlayer">The <see cref="PlayerColor" /> of the player who won due to time forfeit.</param>
	/// <param name="TimedOutPlayer">The <see cref="PlayerColor" /> of the player whose clock ran out.</param>
	public sealed record TimeForfeitWin(PlayerColor WinningPlayer, [UsedImplicitly] PlayerColor TimedOutPlayer)
		: WinOutcome(WinningPlayer)
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="TimeForfeitWin" /> class,
		///     automatically determining the winner based on the player who timed out.
		/// </summary>
		/// <param name="timedOutPlayer">The player whose clock ran out.</param>
		public TimeForfeitWin(PlayerColor timedOutPlayer)
			: this(timedOutPlayer == PlayerColor.White ? PlayerColor.Black : PlayerColor.White, timedOutPlayer) { }
	}

	/// <summary>
	///     Represents that the game outcome is not yet determined.
	///     This is the outcome type used when <see cref="GameStatus" /> is
	///     <see cref="GameStatus.None" />, <see cref="GameStatus.NotStarted" />,
	///     or <see cref="GameStatus.InProgress" />.
	/// </summary>
	public sealed record UndeterminedOutcome : IGameOutcome
	{
		/// <summary> Private constructor to enforce singleton pattern. </summary>
		private UndeterminedOutcome() { }

		/// <summary> Provides a singleton instance for UndeterminedOutcome. </summary>
		public static readonly UndeterminedOutcome INSTANCE = new();
	}

	/// <summary>
	///     Base class for outcomes where there is a definitive winner.
	/// </summary>
	/// <param name="Winner">The <see cref="PlayerColor" /> of the player who won.</param>
	public abstract record WinOutcome([UsedImplicitly] PlayerColor Winner) : IGameOutcome;
}
