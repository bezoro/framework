using Bezoro.Chess.Common.Enums;

namespace Bezoro.Chess.Game
{
	/// <summary>High-level discriminator for outcomes.</summary>
	public enum GameOutcomeKind
	{
		Undetermined,
		Checkmate,
		Resignation,
		TimeForfeitWin,
		TimeForfeitDraw,
		FiftyMoveRule,
		Stalemate,
		Repetition,
		InsufficientMaterial,
		Agreement
	}

	/// <summary>Marker preserved for binary compatibility.</summary>
	public interface IGameOutcome { }

	public sealed record AgreementDraw() : DrawOutcome(GameOutcomeKind.Agreement)
	{
		public static readonly AgreementDraw Instance = new();
	}

	public sealed record Checkmate(PlayerColor Winner)
		: WinOutcome(Winner, GameOutcomeKind.Checkmate);

	/// <summary>Any result that ends in a draw.</summary>
	public abstract record DrawOutcome(GameOutcomeKind Kind) : GameOutcome(Kind);

	public sealed record FiftyMoveRuleDraw() : DrawOutcome(GameOutcomeKind.FiftyMoveRule)
	{
		public static readonly FiftyMoveRuleDraw Instance = new();
	}

	/// <summary>Common base for every specific game-outcome record.</summary>
	public abstract record GameOutcome(GameOutcomeKind Kind) : IGameOutcome;

	public sealed record InsufficientMaterialOnBoardDraw() : DrawOutcome(GameOutcomeKind.InsufficientMaterial)
	{
		public static readonly InsufficientMaterialOnBoardDraw Instance = new();
	}

	public sealed record RepetitionDraw() : DrawOutcome(GameOutcomeKind.Repetition)
	{
		public static readonly RepetitionDraw Instance = new();
	}

	public sealed record Resignation(PlayerColor WinningPlayer, PlayerColor ResigningPlayer)
		: WinOutcome(WinningPlayer, GameOutcomeKind.Resignation)
	{
		public Resignation(PlayerColor resigningPlayer)
			: this(
				resigningPlayer == PlayerColor.White ? PlayerColor.Black : PlayerColor.White,
				resigningPlayer) { }
	}

	public sealed record StalemateDraw() : DrawOutcome(GameOutcomeKind.Stalemate)
	{
		public static readonly StalemateDraw Instance = new();
	}

	public sealed record TimeForfeitInsufficientMaterialDraw(PlayerColor TimedOutPlayer)
		: DrawOutcome(GameOutcomeKind.TimeForfeitDraw);

	public sealed record TimeForfeitWin(PlayerColor WinningPlayer, PlayerColor TimedOutPlayer)
		: WinOutcome(WinningPlayer, GameOutcomeKind.TimeForfeitWin)
	{
		public TimeForfeitWin(PlayerColor timedOutPlayer)
			: this(
				timedOutPlayer == PlayerColor.White ? PlayerColor.Black : PlayerColor.White,
				timedOutPlayer) { }
	}

	public sealed record UndeterminedOutcome() : GameOutcome(GameOutcomeKind.Undetermined)
	{
		public static readonly UndeterminedOutcome Instance = new();
	}

	/// <summary>Any result where one side is declared the winner.</summary>
	public abstract record WinOutcome(PlayerColor Winner, GameOutcomeKind Kind)
		: GameOutcome(Kind);
}
