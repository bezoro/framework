using System.Diagnostics;
using System.Collections.Immutable;
using System.Text;

namespace Bezoro.Chess.UCI.Protocol.Tests.TestHelpers;

/// <summary>
///     Test data builders to simplify creation of complex test objects and reduce duplication.
/// </summary>
public static class TestDataBuilders
{
	/// <summary>
	///     Creates a new FenBuilder with default values.
	/// </summary>
	public static FenBuilder CreateFen() => new();

	/// <summary>
	///     Creates a new PrincipalVariationBuilder with default values.
	/// </summary>
	public static PrincipalVariationBuilder PrincipalVariation() => new();

	/// <summary>
	///     Creates a new SearchResultBuilder with default values.
	/// </summary>
	public static SearchResultBuilder SearchResult() => new();

	/// <summary>
	///     Creates a new ProcessUciTransportBuilder with default values (Stockfish path).
	/// </summary>
	internal static ProcessUciTransportBuilder Transport() => new();

	/// <summary>
	///     Builder for creating Fen instances for testing.
	/// </summary>
	public class FenBuilder
	{
		private string _raw = TestConstants.STANDARD_FEN;

		public Fen? Build() => Fen.Parse(_raw);

		public Fen BuildOrThrow()
		{
			var fen = Build();
			if (!fen.HasValue)
				throw new ArgumentException($"Invalid FEN: {_raw}", nameof(_raw));

			return fen.Value;
		}

		public FenBuilder WithAfterE2E4()
		{
			_raw = TestConstants.AFTER_E2_E4_FEN;
			return this;
		}

		public FenBuilder WithItalianGame()
		{
			_raw = TestConstants.ITALIAN_GAME_FEN;
			return this;
		}

		public FenBuilder WithRaw(string raw)
		{
			_raw = raw;
			return this;
		}

		public FenBuilder WithStalemate()
		{
			_raw = TestConstants.STALEMATE_FEN;
			return this;
		}

		public FenBuilder WithStandardPosition()
		{
			_raw = TestConstants.STANDARD_FEN;
			return this;
		}

		public FenBuilder WithWhiteMateInOne()
		{
			_raw = TestConstants.WHITE_MATE_IN_ONE_FEN;
			return this;
		}
	}

	/// <summary>
	///     Builder for creating PrincipalVariation instances for testing.
	/// </summary>
	public class PrincipalVariationBuilder
	{
		private int?                  _scoreCp = 34;
		private int?                  _scoreMate;
		private ImmutableArray<string> _moves    = ["e2e4", "e7e5"];
		private string                _rawPv    = "e2e4 e7e5";
		private uint                  _depth    = 10;
		private uint                  _multiPv  = 1;
		private uint                  _nodes    = 1000;
		private uint                  _nps      = 5000;
		private uint                  _selDepth = 10;
		private uint                  _tbHits   = 2;
		private uint                  _time     = 50;

		public PrincipalVariation Build() =>
			new(
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
				_rawPv
			);

		public PrincipalVariationBuilder WithDepth(uint depth)
		{
			_depth = depth;
			return this;
		}

		public PrincipalVariationBuilder WithMoves(params string[] moves)
		{
			_moves = [.. moves];
			_rawPv = string.Join(" ", moves);
			return this;
		}

		public PrincipalVariationBuilder WithMultiPv(uint multiPv)
		{
			_multiPv = multiPv;
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

		public PrincipalVariationBuilder WithRawPv(string rawPv)
		{
			_rawPv = rawPv;
			return this;
		}

		public PrincipalVariationBuilder WithScoreCp(int? scoreCp)
		{
			_scoreCp   = scoreCp;
			_scoreMate = null; // Clear mate when setting CP
			return this;
		}

		public PrincipalVariationBuilder WithScoreMate(int? scoreMate)
		{
			_scoreMate = scoreMate;
			_scoreCp   = null; // Clear CP when setting mate
			return this;
		}

		public PrincipalVariationBuilder WithSelDepth(uint selDepth)
		{
			_selDepth = selDepth;
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
	}

	/// <summary>
	///     Builder for creating SearchResult instances for testing.
	/// </summary>
	public class SearchResultBuilder
	{
		private ImmutableArray<PrincipalVariation> _principalVariations = [];
		private string                            _bestMove            = "e2e4";
		private string                            _ponderMove          = string.Empty;
		private uint                              _multiPvValue        = 1;
		private uint                              _reachedDepth        = 10;
		private uint                              _reachedSelDepth     = 10;
		private uint                              _totalNodesSearched  = 1000;
		private uint                              _totalSearchTimeMs   = 50;
		private uint                              _totalTbHits;

		public SearchResult Build()
		{
			// If no PVs provided, create a default one
			if (_principalVariations.IsDefaultOrEmpty)
				_principalVariations =
				[
					PrincipalVariation()
						.WithMoves(_bestMove)
						.WithDepth(_reachedDepth)
						.Build()
				];

			return new(
				_reachedDepth,
				_reachedSelDepth,
				_multiPvValue,
				_totalNodesSearched,
				_totalTbHits,
				_totalSearchTimeMs,
				_principalVariations,
				_bestMove,
				_ponderMove
			);
		}

		public SearchResultBuilder WithBestMove(string bestMove)
		{
			_bestMove = bestMove;
			return this;
		}

		public SearchResultBuilder WithMultiPvValue(uint multiPv)
		{
			_multiPvValue = multiPv;
			return this;
		}

		public SearchResultBuilder WithPonderMove(string ponderMove)
		{
			_ponderMove = ponderMove;
			return this;
		}

		public SearchResultBuilder WithPrincipalVariations(params PrincipalVariation[] pvs)
		{
			_principalVariations = [.. pvs];
			return this;
		}

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

		public SearchResultBuilder WithTotalNodesSearched(uint nodes)
		{
			_totalNodesSearched = nodes;
			return this;
		}

		public SearchResultBuilder WithTotalSearchTimeMs(uint timeMs)
		{
			_totalSearchTimeMs = timeMs;
			return this;
		}

		public SearchResultBuilder WithTotalTbHits(uint tbHits)
		{
			_totalTbHits = tbHits;
			return this;
		}
	}

	/// <summary>
	///     Builder for creating ProcessUciTransport instances for testing.
	///     Provides a fluent API and preset configurations for common test scenarios.
	/// </summary>
	internal class ProcessUciTransportBuilder
	{
		private ProcessUciTransportOptions _options = new();
		private string                     _path    = TestResourcePaths.STOCKFISH_PATH;
		private string?                    _workingDirectory;
		private string[]?                  _arguments;

		/// <summary>Builds a new ProcessUciTransport instance with the configured settings.</summary>
		public ProcessUciTransport Build() => new(_path, _arguments, _workingDirectory, _options);

		/// <summary>Creates a builder preset for backpressure testing.</summary>
		public static ProcessUciTransportBuilder ForBackpressureTest() => new ProcessUciTransportBuilder()
																		  .WithChannelCapacity(1)
																		  .WithDisableWriteLoop();

		/// <summary>Creates a builder preset for multiple readers testing.</summary>
		public static ProcessUciTransportBuilder ForMultipleReaders() => new ProcessUciTransportBuilder()
			.WithSingleReader(false);

		/// <summary>Creates a builder preset for Stockfish with default settings.</summary>
		public static ProcessUciTransportBuilder ForStockfish() => new();

		/// <summary>Sets the command-line arguments.</summary>
		public ProcessUciTransportBuilder WithArguments(params string[] args)
		{
			_arguments = args;
			return this;
		}

		/// <summary>Sets the channel capacity for outgoing commands.</summary>
		public ProcessUciTransportBuilder WithChannelCapacity(int capacity)
		{
			_options = _options with { ChannelCapacity = capacity };
			return this;
		}

		/// <summary>Disables the write loop (test-only).</summary>
		public ProcessUciTransportBuilder WithDisableWriteLoop(bool disable = true)
		{
			_options = _options with { DisableWriteLoop = disable };
			return this;
		}

		/// <summary>Sets the flush batch size.</summary>
		public ProcessUciTransportBuilder WithFlushBatchSize(int batchSize)
		{
			_options = _options with { FlushBatchSize = batchSize };
			return this;
		}

		/// <summary>Sets the maximum line length.</summary>
		public ProcessUciTransportBuilder WithMaxLineLength(int maxLength)
		{
			_options = _options with { MaxLineLength = maxLength };
			return this;
		}

		/// <summary>Sets the on-quit-sent callback (test-only).</summary>
		public ProcessUciTransportBuilder WithOnQuitSent(Action callback)
		{
			_options = _options with { OnQuitSent = callback };
			return this;
		}

		/// <summary>Replaces all options with the specified options object.</summary>
		public ProcessUciTransportBuilder WithOptions(ProcessUciTransportOptions options)
		{
			_options = options;
			return this;
		}

		/// <summary>Sets whether to allow single writer only.</summary>
		public ProcessUciTransportBuilder WithOutgoingSingleWriter(bool single = true)
		{
			_options = _options with { OutgoingSingleWriter = single };
			return this;
		}

		/// <summary>Sets the executable path for the transport.</summary>
		public ProcessUciTransportBuilder WithPath(string path)
		{
			_path = path;
			return this;
		}

		/// <summary>Sets the quit grace period.</summary>
		public ProcessUciTransportBuilder WithQuitGracePeriod(TimeSpan period)
		{
			_options = _options with { QuitGracePeriod = period };
			return this;
		}

		/// <summary>Sets whether to redirect standard error.</summary>
		public ProcessUciTransportBuilder WithRedirectStandardError(bool redirect = true)
		{
			_options = _options with { RedirectStandardError = redirect };
			return this;
		}

		/// <summary>Sets whether to send quit on dispose.</summary>
		public ProcessUciTransportBuilder WithSendQuitOnDispose(bool send = true)
		{
			_options = _options with { SendQuitOnDispose = send };
			return this;
		}

		/// <summary>Sets whether to send quit on stop.</summary>
		public ProcessUciTransportBuilder WithSendQuitOnStop(bool send = true)
		{
			_options = _options with { SendQuitOnStop = send };
			return this;
		}

		/// <summary>Sets whether to allow single reader only.</summary>
		public ProcessUciTransportBuilder WithSingleReader(bool single = true)
		{
			_options = _options with { SingleReader = single };
			return this;
		}

		/// <summary>Sets the small timeout spin iterations.</summary>
		public ProcessUciTransportBuilder WithSmallTimeoutSpinIterations(int iterations)
		{
			_options = _options with { SmallTimeoutSpinIterations = iterations };
			return this;
		}

		/// <summary>Sets the stderr encoding.</summary>
		public ProcessUciTransportBuilder WithStderrEncoding(Encoding encoding)
		{
			_options = _options with { StderrEncoding = encoding };
			return this;
		}

		/// <summary>Sets the stdin encoding.</summary>
		public ProcessUciTransportBuilder WithStdinEncoding(Encoding encoding)
		{
			_options = _options with { StdinEncoding = encoding };
			return this;
		}

		/// <summary>Sets the stdout encoding.</summary>
		public ProcessUciTransportBuilder WithStdoutEncoding(Encoding encoding)
		{
			_options = _options with { StdoutEncoding = encoding };
			return this;
		}

		/// <summary>Sets the teardown timeout.</summary>
		public ProcessUciTransportBuilder WithTeardownTimeout(TimeSpan timeout)
		{
			_options = _options with { TeardownTimeout = timeout };
			return this;
		}

		/// <summary>Sets whether to validate commands before sending.</summary>
		public ProcessUciTransportBuilder WithValidateCommands(bool validate = true)
		{
			_options = _options with { ValidateCommands = validate };
			return this;
		}

		/// <summary>Sets the working directory for the process.</summary>
		public ProcessUciTransportBuilder WithWorkingDirectory(string dir)
		{
			_workingDirectory = dir;
			return this;
		}

		/// <summary>Sets a test-only override for process-exit waits.</summary>
		internal ProcessUciTransportBuilder WithWaitForProcessExitOverride(
			Func<Process, CancellationToken, Task> waitForProcessExitAsync)
		{
			_options = _options with { WaitForProcessExitAsyncOverride = waitForProcessExitAsync };
			return this;
		}
	}
}
