using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using Bezoro.Chess.UCI.Protocol.Internal;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.Internal;

[TestSubject(typeof(LocalFenRules))]
public sealed class LocalFenRulesTests
{
	[Fact]
	public void TryCreatePendingPromotion_WhenAllChoicesAreLegal_ShouldPreserveCanonicalOrder()
	{
		var fen = Fen.Parse("7k/P7/8/8/8/8/8/K7 w - - 0 1")!.Value;
		var legalMoves = fen.GetLegalMoves();

		bool created = LocalFenRules.TryCreatePendingPromotion(fen, "a7a8", legalMoves, out var request);

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
	[InlineData("5b1k/8/8/8/8/8/8/K1B5 w - - 0 1", true)]
	[InlineData("6bk/8/8/8/8/8/8/K1B5 w - - 0 1", false)]
	[InlineData("7k/8/8/8/8/8/8/KR6 w - - 0 1", false)]
	[InlineData("7k/8/8/8/8/8/P7/K7 w - - 0 1", false)]
	public void HasInsufficientMaterial_WhenMaterialVaries_ShouldReturnCurrentAdjudication(
		string rawFen,
		bool   expected)
	{
		var fen = Fen.Parse(rawFen)!.Value;

		bool result = LocalFenRules.HasInsufficientMaterial(fen);

		result.Should().Be(expected);
	}

	[Fact]
	public void BuildRepetitionKey_WhenOnlyMoveClocksDiffer_ShouldReturnSameKey()
	{
		var first = Fen.Parse("4k3/8/8/8/8/8/8/4K3 w Kq e3 0 1")!.Value;
		var second = Fen.Parse("4k3/8/8/8/8/8/8/4K3 w Kq e3 47 91")!.Value;

		string firstKey = LocalFenRules.BuildRepetitionKey(first);
		string secondKey = LocalFenRules.BuildRepetitionKey(second);

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

		LocalFenRules.BuildRepetitionKey(changed)
			.Should().NotBe(LocalFenRules.BuildRepetitionKey(baseline));
	}
}
