namespace Bezoro.Chess.UCI.Protocol.ConsoleDemo;

internal static class ConsolePromptLayout
{
	public static bool CanRenderInPlace(int frameHeight, int bufferHeight) =>
		frameHeight > 0 && bufferHeight > 0 && frameHeight <= bufferHeight;

	public static bool CanAnchorFrame(int cursorTop, int bufferHeight, int frameHeight) =>
		CanRenderInPlace(frameHeight, bufferHeight) && cursorTop >= 0 && cursorTop + frameHeight <= bufferHeight;

	public static int GetSafeTopRow(int cursorTop, int bufferHeight, int frameHeight)
	{
		if (bufferHeight <= 0)
			throw new ArgumentOutOfRangeException(nameof(bufferHeight));

		if (frameHeight <= 0)
			throw new ArgumentOutOfRangeException(nameof(frameHeight));

		int maxTopRow = Math.Max(0, bufferHeight - frameHeight);
		return Math.Clamp(cursorTop, 0, maxTopRow);
	}

	public static int GetTopRowFromBottomRow(int bottomRow, int bufferHeight, int frameHeight)
	{
		if (bufferHeight <= 0)
			throw new ArgumentOutOfRangeException(nameof(bufferHeight));

		if (frameHeight <= 0)
			throw new ArgumentOutOfRangeException(nameof(frameHeight));

		return GetSafeTopRow(bottomRow - frameHeight + 1, bufferHeight, frameHeight);
	}
}
