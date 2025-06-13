using Bezoro.Chess.Application.Features.PlayGame;

namespace Bezoro.Chess.Domain.Rules
{
	public static class GameManagerHelper
	{
		public static bool IsFinished(this GameOutcome outcome) =>
			outcome is not (GameOutcome.Ongoing or GameOutcome.None);
	}
}
