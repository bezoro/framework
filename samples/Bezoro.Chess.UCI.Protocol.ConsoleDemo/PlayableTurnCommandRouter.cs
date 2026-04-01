using Bezoro.Chess.UCI.Protocol.API.Types;

namespace Bezoro.Chess.UCI.Protocol.ConsoleDemo;

internal static class PlayableTurnCommandRouter
{
	public static bool ShouldHandleInsidePrompt(PlayableMatchCommandKind commandKind) =>
		commandKind is PlayableMatchCommandKind.Moves or PlayableMatchCommandKind.History;

	public static bool ShouldValidateAsMove(PlayableMatchCommandKind commandKind) =>
		commandKind is PlayableMatchCommandKind.Move or PlayableMatchCommandKind.Invalid;
}
