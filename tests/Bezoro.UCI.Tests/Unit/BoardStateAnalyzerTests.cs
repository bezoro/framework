using Bezoro.UCI.Domain;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Unit;

[TestSubject(typeof(BoardStateAnalyzer))]
public class BoardStateAnalyzerTests : UCITestsBase
{
	private BoardStateAnalyzer?   _boardStateAnalyzer;
	private EngineCommandSender?  _engineCommandSender;
	private EngineOutputParser?   _engineOutputParser;
	private EngineProcessManager? _engineProcessManager;

	public override async Task InitializeAsync()
	{
		_engineProcessManager = new EngineProcessManager(StockfishPath);
		_engineOutputParser   = new EngineOutputParser(_engineProcessManager);
		_engineCommandSender  = new EngineCommandSender(_engineProcessManager, _engineOutputParser);
		_boardStateAnalyzer   = new BoardStateAnalyzer(_engineCommandSender, _engineOutputParser);
		Connector = new UCIConnector(StockfishPath, _engineProcessManager, _engineCommandSender, _engineOutputParser,
			_boardStateAnalyzer);

		_engineProcessManager.StartEngine();
		await Connector.SetPositionAsync();

	}

	[Fact]
	public async Task FindKingSquare_WhenValidBoardState_ShouldReturnCorrectKing()
	{
		// Act
		string? whiteKingSquare = await _boardStateAnalyzer!.FindKingSquare('w');
		string? blackKingSquare = await _boardStateAnalyzer.FindKingSquare('b');

		// Assert
		// In the FEN position above, white king is on e1 and black king is on e8
		Assert.NotNull(whiteKingSquare);
		Assert.NotNull(blackKingSquare);
		Assert.Equal("e1", whiteKingSquare);
		Assert.Equal("e8", blackKingSquare);
	}

	[Fact]
	public void GetCurrentFENAsync_WhenValidState_ShouldReturnCorrectFENString() { }

	[Theory]
	[InlineData('w', "White player should have 20 moves in starting position")]
	[InlineData('b', "Black player should have 20 moves in starting position")]
	public async Task GetMovesForPlayerAsync_WhenValidState_ReturnsMoves(char playerColor, string testDescription)
	{
		// Arrange
		const string testFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
		await Connector!.Value.SetPositionAsync(testFen);

		string[]   fenParts                      = testFen.Split(' ');
		const char activeColor                   = 'w'; // White to move in starting position
		const int  expectedStartingPositionMoves = 20;  // 16 pawn moves + 4 knight moves

		// Act
		List<string> moves = await _boardStateAnalyzer!.GetMovesForPlayerAsync(playerColor);

		// Assert
		Assert.NotNull(moves);
		Assert.NotEmpty(moves);
		Assert.Equal(expectedStartingPositionMoves, moves.Count);
	}

	[Fact]
	public async Task IsCheckmateAsync_WhenValidBoardState_ReturnsTrue()
	{
		// Arrange
		// Checkmate position: Black king on a8 is checkmated by the White queen on a1,
		// which is supported by the White king on b6. It is Black's turn to move.
		const string checkmateFen = "4R1k1/5ppp/8/8/8/8/8/4K3 b - - 0 1";
		await Connector!.Value.SetPositionAsync(checkmateFen);

		// Act
		// Check if the current position is checkmate
		bool isCheckmate = await _boardStateAnalyzer!.IsCheckmateAsync();

		// Assert
		// The position should be detected as checkmate
		Assert.True(isCheckmate);
	}

	[Fact]
	public async Task IsKingInCheckAsync_WhenInCheck_ReturnsTrue()
	{
		// Arrange
		// Position where black king on e8 is in check from white queen on e2
		const string checkFen = "r3k2r/pppp1ppp/8/8/8/8/PPPPQPPP/R3K2R w KQkq - 0 1";
		await Connector!.Value.SetPositionAsync(checkFen);

		// Act
		// Check if the king is in check for the current position
		bool isKingInCheck = await _boardStateAnalyzer!.IsKingInCheckAsync();

		// Assert
		Assert.True(isKingInCheck, "The king should be in check in the given position.");
	}

	[Fact]
	public async Task IsSquareAttackedByAsync_SimplePawnAttack_ReturnsTrue()
	{
		// Arrange
		// Simple position: White pawn on e4, Black pawn on d5
		// The white pawn on e4 attacks the black pawn on d5
		const string simplePawnAttackFen = "4k3/8/8/3p4/4P3/8/8/4K3 w - - 0 1";
		const char   attackerColor       = 'w';
		await Connector.SetPositionAsync(simplePawnAttackFen);

		// Act
		// Check if the d5 square (where the black pawn is) is attacked by white
		bool isD5Attacked = await Connector.IsSquareAttackedAsync("d5", attackerColor);

		// Assert
		Assert.True(isD5Attacked, "The d5 square should be attacked by the white pawn on e4.");
	}

	[Fact]
	public async Task IsStalemateAsync_WhenValidBoardState_ShouldReturnTrue()
	{
		// Arrange
		// True stalemate: Black king on a8, white king on a6, white pawn on a7
		// Black king cannot move anywhere and is not in check
		const string stalemateFen = "k7/P7/K7/8/8/8/8/8 b - - 0 1";
		await Connector!.SetPositionAsync(stalemateFen);

		// Act
		// Check if the current position is a stalemate
		bool isStalemate = await Connector.IsStalemateAsync();

		// Assert
		// The position should be detected as stalemate
		Assert.True(isStalemate);
	}

	[Fact]
	public async Task ParseCurrentFENAsync_WhenValidFEN_ShouldReturnFENInfoObject()
	{
		var fenInfo = await _boardStateAnalyzer.ParseCurrentFenAsync();

		Assert.NotNull(fenInfo);
		Assert.Equal("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR", fenInfo.FenParts[0]);
		Assert.Equal('w',                                           fenInfo.ActiveColor);
		Assert.Equal("KQkq",                                        fenInfo.CastlingRights);
		Assert.Equal("-",                                           fenInfo.EnPassantTarget);
		Assert.Equal(0,                                             fenInfo.HalfmoveClock);
		Assert.Equal(1,                                             fenInfo.FullmoveNumber);
	}
}
