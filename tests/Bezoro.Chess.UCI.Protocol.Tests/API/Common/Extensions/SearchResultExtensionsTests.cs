using System.Collections.Immutable;
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Common.Extensions;

[TestSubject(typeof(SearchResultExtensions))]
public class SearchResultExtensionsTests
{
	[Fact]
	public void ToDisplayString_WhenSearchResultHasDepthEvalAndPv_ShouldFormatCompactSummary()
	{
		var result = new SearchResult(
			ReachedDepth: 18,
			ReachedSelDepth: 24,
			MultiPvValue: 1,
			TotalNodesSearched: 100,
			TotalTbHits: 0,
			TotalSearchTimeMs: 10,
			PrincipalVariations:
			[
				new PrincipalVariation(
					Depth: 18,
					SelDepth: 24,
					MultiPv: 1,
					ScoreCp: 42,
					ScoreMate: null,
					Nodes: 100,
					Nps: 1000,
					TbHits: 0,
					Time: 10,
					Moves: ImmutableArray.Create("e2e4", "e7e5", "g1f3", "b8c6", "f1b5", "a7a6", "b5a4"),
					RawPv: "e2e4 e7e5 g1f3 b8c6 f1b5 a7a6 b5a4"
				)
			],
			BestMove: "e2e4",
			PonderMove: "e7e5"
		);

		result.ToDisplayString().Should().Be(" (depth 18, eval 42 cp, pv e2e4 e7e5 g1f3 b8c6 f1b5 a7a6)");
	}
}
