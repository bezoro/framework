using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using Bezoro.Chess.UCI.Protocol.Internal;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.Internal;

[TestSubject(typeof(LocalPositionRules))]
public sealed class LocalPositionRulesTests
{
	[Fact]
	public void TryCreatePendingPromotion_WhenAllChoicesAreLegal_ShouldPreserveCanonicalOrder()
	{
		var fen = Fen.Parse("7k/P7/8/8/8/8/8/K7 w - - 0 1")!.Value;
		var legalMoves = fen.GetLegalMoves();

		bool created = LocalPositionRules.TryCreatePendingPromotion(fen, "a7a8", legalMoves, out var request);

		created.Should().BeTrue();
		request.MovePrefix.Should().Be("a7a8");
		request.AllowedPromotionPieces.Should().Equal('q', 'r', 'b', 'n');
	}

	[Theory]
	[InlineData("7k/8/8/8/8/8/8/K7 w - - 0 1", true)]
	[InlineData("7k/8/8/8/8/8/8/KN6 w - - 0 1", true)]
	[InlineData("7k/8/8/8/8/8/8/KB6 w - - 0 1", true)]
	[InlineData("7k/8/8/8/8/8/8/KNN5 w - - 0 1", false)]
	[InlineData("7k/8/8/8/8/8/8/KBB5 w - - 0 1", false)]
	[InlineData("7k/8/8/8/8/8/8/KBN5 w - - 0 1", false)]
	[InlineData("5b1k/8/8/8/8/8/8/K1B5 w - - 0 1", true)]
	[InlineData("6bk/8/8/8/8/8/8/K1B5 w - - 0 1", false)]
	[InlineData("7k/8/8/5b2/8/3B4/8/KB6 w - - 0 1", true)]
	[InlineData("7k/8/8/4b3/8/3B4/8/KB6 w - - 0 1", false)]
	[InlineData("7k/8/8/8/8/8/8/KR6 w - - 0 1", false)]
	[InlineData("7k/8/8/8/8/8/8/KQ6 w - - 0 1", false)]
	[InlineData("7k/8/8/8/8/8/P7/K7 w - - 0 1", false)]
	public void HasInsufficientMaterial_WhenMaterialVaries_ShouldReturnExpectedAdjudication(
		string rawFen,
		bool   expected)
	{
		var fen = Fen.Parse(rawFen)!.Value;

		bool result = LocalPositionRules.HasInsufficientMaterial(fen);

		result.Should().Be(expected);
	}

	[Fact]
	public void BuildRepetitionKey_WhenOnlyMoveClocksDiffer_ShouldReturnSameKey()
	{
		var first = Fen.Parse("4k3/8/8/8/8/8/8/4K3 w Kq e3 0 1")!.Value;
		var second = Fen.Parse("4k3/8/8/8/8/8/8/4K3 w Kq e3 47 91")!.Value;

		string firstKey = LocalPositionRules.BuildRepetitionKey(first);
		string secondKey = LocalPositionRules.BuildRepetitionKey(second);

		firstKey.Should().Be("4k3/8/8/8/8/8/8/4K3 w Kq e3");
		secondKey.Should().Be(firstKey);
	}

	[Theory]
	[InlineData("4k3/8/8/8/8/8/8/4K3 b Kq e3 0 1")]
	[InlineData("4k3/8/8/8/8/8/8/4K3 w Qq e3 0 1")]
	[InlineData("4k3/8/8/8/8/8/8/4K3 w Kq d3 0 1")]
	public void BuildRepetitionKey_WhenPositionIdentityFieldDiffers_ShouldReturnDifferentKey(string rawFen)
	{
		var baseline = Fen.Parse("4k3/8/8/8/8/8/8/4K3 w Kq e3 0 1")!.Value;
		var changed = Fen.Parse(rawFen)!.Value;

		LocalPositionRules.BuildRepetitionKey(changed)
			.Should().NotBe(LocalPositionRules.BuildRepetitionKey(baseline));
	}

	[Fact]
	public void ClassifyMovesFully_WhenCancellationArrivesBetweenMoves_ShouldStopBeforeNextMove()
	{
		using var cancellation = new CancellationTokenSource();

		Action classify = () => LocalPositionRules.ClassifyMovesFully(
			Fen.Default,
			CancelBeforeSecondMove(cancellation),
			cancellation.Token
		);

		classify.Should().Throw<OperationCanceledException>();
	}

	private static IEnumerable<string> CancelBeforeSecondMove(CancellationTokenSource cancellation)
	{
		yield return "e2e4";
		cancellation.Cancel();
		yield return "not-a-move";
	}
}
