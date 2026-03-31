using Bezoro.Chess.UCI.Protocol.ConsoleDemo;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.ConsoleDemo;

[TestSubject(typeof(ConsolePromptLayout))]
public class ConsolePromptLayoutTests
{
	[Theory]
	[InlineData(10, 40, 5, 10)]
	[InlineData(40, 40, 5, 35)]
	[InlineData(39, 40, 5, 35)]
	[InlineData(0, 40, 5, 0)]
	public void GetSafeTopRow_WhenFrameFitsInBuffer_ShouldClampIntoVisibleRange(
		int cursorTop,
		int bufferHeight,
		int frameHeight,
		int expectedTopRow)
	{
		ConsolePromptLayout.GetSafeTopRow(cursorTop, bufferHeight, frameHeight).Should().Be(expectedTopRow);
	}

	[Theory]
	[InlineData(12, 12, true)]
	[InlineData(11, 12, true)]
	[InlineData(13, 12, false)]
	public void CanRenderInPlace_WhenFrameHeightComparedToBufferHeight_ShouldReportCapacity(
		int frameHeight,
		int bufferHeight,
		bool expected)
	{
		ConsolePromptLayout.CanRenderInPlace(frameHeight, bufferHeight).Should().Be(expected);
	}

	[Theory]
	[InlineData(10, 40, 5, true)]
	[InlineData(35, 40, 5, true)]
	[InlineData(36, 40, 5, false)]
	public void CanAnchorFrame_WhenCursorTopAndFrameHeightComparedToBufferHeight_ShouldRequireFrameToFitBelowCursor(
		int cursorTop,
		int bufferHeight,
		int frameHeight,
		bool expected)
	{
		ConsolePromptLayout.CanAnchorFrame(cursorTop, bufferHeight, frameHeight).Should().Be(expected);
	}

	[Theory]
	[InlineData(14, 40, 5, 10)]
	[InlineData(39, 40, 5, 35)]
	[InlineData(0, 40, 1, 0)]
	public void GetTopRowFromBottomRow_WhenFrameWasFreshlyRendered_ShouldLocateOwnedFrameRegion(
		int bottomRow,
		int bufferHeight,
		int frameHeight,
		int expectedTopRow)
	{
		ConsolePromptLayout.GetTopRowFromBottomRow(bottomRow, bufferHeight, frameHeight).Should().Be(expectedTopRow);
	}
}
