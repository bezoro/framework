using System.Collections.Generic;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Tests.TestHelpers;

namespace Bezoro.UCI.Tests.TestHelpers;

/// <summary>
/// Test data builders to simplify creation of complex test objects and reduce duplication.
/// </summary>
public static class TestDataBuilders
{
	#region PrincipalVariation Builder

	/// <summary>
	/// Builder for creating PrincipalVariation instances for testing.
	/// </summary>
	public class PrincipalVariationBuilder
	{
		private uint _depth = 10;
		private uint _selDepth = 10;
		private uint _multiPv = 1;
		private int? _scoreCp = 34;
		private int? _scoreMate;
		private uint _nodes = 1000;
		private uint _nps = 5000;
		private uint _tbHits = 2;
		private uint _time = 50;
		private IReadOnlyList<string> _moves = new[] { "e2e4", "e7e5" };
		private string _rawPv = "e2e4 e7e5";

		public PrincipalVariationBuilder WithDepth(uint depth)
		{
			_depth = depth;
			return this;
		}

		public PrincipalVariationBuilder WithSelDepth(uint selDepth)
		{
			_selDepth = selDepth;
			return this;
		}

		public PrincipalVariationBuilder WithMultiPv(uint multiPv)
		{
			_multiPv = multiPv;
			return this;
		}

		public PrincipalVariationBuilder WithScoreCp(int? scoreCp)
		{
			_scoreCp = scoreCp;
			_scoreMate = null; // Clear mate when setting CP
			return this;
		}

		public PrincipalVariationBuilder WithScoreMate(int? scoreMate)
		{
			_scoreMate = scoreMate;
			_scoreCp = null; // Clear CP when setting mate
			return this;
		}

		public PrincipalVariationBuilder WithNodes(uint nodes)
		{
			_nodes = nodes;
			return this;
		}

		public PrincipalVariationBuilder WithNps(uint nps)
		{
			_nps = nps;
			return this;
		}

		public PrincipalVariationBuilder WithTbHits(uint tbHits)
		{
			_tbHits = tbHits;
			return this;
		}

		public PrincipalVariationBuilder WithTime(uint time)
		{
			_time = time;
			return this;
		}

		public PrincipalVariationBuilder WithMoves(params string[] moves)
		{
			_moves = moves;
			_rawPv = string.Join(" ", moves);
			return this;
		}

		public PrincipalVariationBuilder WithRawPv(string rawPv)
		{
			_rawPv = rawPv;
			return this;
		}

		public PrincipalVariation Build()
		{
			return new PrincipalVariation(
				_depth,
				_selDepth,
				_multiPv,
				_scoreCp,
				_scoreMate,
				_nodes,
				_nps,
				_tbHits,
				_time,
				_moves,
				_rawPv);
		}
	}

	/// <summary>
	/// Creates a new PrincipalVariationBuilder with default values.
	/// </summary>
	public static PrincipalVariationBuilder PrincipalVariation() => new();

	#endregion

	#region SearchResult Builder

	/// <summary>
	/// Builder for creating SearchResult instances for testing.
	/// </summary>
	public class SearchResultBuilder
	{
		private uint _reachedDepth = 10;
		private uint _reachedSelDepth = 10;
		private uint _multiPvValue = 1;
		private uint _totalNodesSearched = 1000;
		private uint _totalTbHits = 0;
		private uint _totalSearchTimeMs = 50;
		private IReadOnlyList<PrincipalVariation> _principalVariations = new List<PrincipalVariation>();
		private string _bestMove = "e2e4";
		private string _ponderMove = string.Empty;

		public SearchResultBuilder WithReachedDepth(uint depth)
		{
			_reachedDepth = depth;
			return this;
		}

		public SearchResultBuilder WithReachedSelDepth(uint selDepth)
		{
			_reachedSelDepth = selDepth;
			return this;
		}

		public SearchResultBuilder WithMultiPvValue(uint multiPv)
		{
			_multiPvValue = multiPv;
			return this;
		}

		public SearchResultBuilder WithTotalNodesSearched(uint nodes)
		{
			_totalNodesSearched = nodes;
			return this;
		}

		public SearchResultBuilder WithTotalTbHits(uint tbHits)
		{
			_totalTbHits = tbHits;
			return this;
		}

		public SearchResultBuilder WithTotalSearchTimeMs(uint timeMs)
		{
			_totalSearchTimeMs = timeMs;
			return this;
		}

		public SearchResultBuilder WithPrincipalVariations(params PrincipalVariation[] pvs)
		{
			_principalVariations = pvs;
			return this;
		}

		public SearchResultBuilder WithBestMove(string bestMove)
		{
			_bestMove = bestMove;
			return this;
		}

		public SearchResultBuilder WithPonderMove(string ponderMove)
		{
			_ponderMove = ponderMove;
			return this;
		}

		public SearchResult Build()
		{
			// If no PVs provided, create a default one
			if (_principalVariations.Count == 0)
			{
				_principalVariations = new[]
				{
					TestDataBuilders.PrincipalVariation()
						.WithMoves(_bestMove)
						.WithDepth(_reachedDepth)
						.Build()
				};
			}

			return new SearchResult(
				_reachedDepth,
				_reachedSelDepth,
				_multiPvValue,
				_totalNodesSearched,
				_totalTbHits,
				_totalSearchTimeMs,
				_principalVariations,
				_bestMove,
				_ponderMove);
		}
	}

	/// <summary>
	/// Creates a new SearchResultBuilder with default values.
	/// </summary>
	public static SearchResultBuilder SearchResult() => new();

	#endregion

	#region MoveScore Builder

	/// <summary>
	/// Builder for creating MoveScore instances for testing.
	/// </summary>
	public class MoveScoreBuilder
	{
		private int? _scoreCp;
		private int? _scoreMate;

		public MoveScoreBuilder WithScoreCp(int cp)
		{
			_scoreCp = cp;
			_scoreMate = null;
			return this;
		}

		public MoveScoreBuilder WithScoreMate(int mate)
		{
			_scoreMate = mate;
			_scoreCp = null;
			return this;
		}

		public MoveScore Build()
		{
			// MoveScore is a record struct with no public constructor
			// Use static factory methods instead
			if (_scoreMate.HasValue)
				return MoveScore.FromMate(_scoreMate.Value);
			if (_scoreCp.HasValue)
				return MoveScore.FromCp(_scoreCp.Value);
			return default;
		}
	}

	/// <summary>
	/// Creates a new MoveScoreBuilder with default values.
	/// </summary>
	public static MoveScoreBuilder CreateMoveScore() => new();

	#endregion

	#region Fen Builder

	/// <summary>
	/// Builder for creating Fen instances for testing.
	/// </summary>
	public class FenBuilder
	{
		private string _raw = TestConstants.StandardFen;

		public FenBuilder WithRaw(string raw)
		{
			_raw = raw;
			return this;
		}

		public FenBuilder WithStandardPosition()
		{
			_raw = TestConstants.StandardFen;
			return this;
		}

		public FenBuilder WithAfterE2E4()
		{
			_raw = TestConstants.AfterE2E4Fen;
			return this;
		}

		public FenBuilder WithWhiteMateInOne()
		{
			_raw = TestConstants.WhiteMateInOneFen;
			return this;
		}

		public FenBuilder WithStalemate()
		{
			_raw = TestConstants.StalemateFen;
			return this;
		}

		public FenBuilder WithItalianGame()
		{
			_raw = TestConstants.ItalianGameFen;
			return this;
		}

		public Fen? Build()
		{
			return Fen.Parse(_raw);
		}

		public Fen BuildOrThrow()
		{
			var fen = Build();
			if (!fen.HasValue)
				throw new ArgumentException($"Invalid FEN: {_raw}", nameof(_raw));
			return fen.Value;
		}
	}

	/// <summary>
	/// Creates a new FenBuilder with default values.
	/// </summary>
	public static FenBuilder CreateFen() => new();

	#endregion
}

