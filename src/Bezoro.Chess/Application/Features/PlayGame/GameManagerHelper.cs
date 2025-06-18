namespace Bezoro.Chess.Application.Features.PlayGame
{
	public static class GameManagerHelper
	{
		public static bool IsFinished(this GameOutcome outcome) =>
			outcome is not (GameOutcome.Ongoing or GameOutcome.None);
	}
}
