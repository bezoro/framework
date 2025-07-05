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
		await base.InitializeAsync();
		_engineProcessManager = new EngineProcessManager(StockfishPath);
		_engineCommandSender  = new EngineCommandSender(_engineProcessManager);
		_engineOutputParser   = new EngineOutputParser(_engineProcessManager);
		_boardStateAnalyzer   = new BoardStateAnalyzer(_engineCommandSender, _engineOutputParser);
	}

	[Fact]
	public void FindKingSquare_WhenValidBoardState_ShouldReturnCorrectKing()
	{
		// Arrange
		const string customFen = "r3k2r/pppppppp/8/8/8/8/PPPPPPPP/R3K2R w KQkq - 0 1";

		_boardStateAnalyzer = new BoardStateAnalyzer(_engineCommandSender, _engineOutputParser);

		// Act
		string? whiteKingSquare = _boardStateAnalyzer!.FindKingSquare(customFen, 'w');
		string? blackKingSquare = _boardStateAnalyzer.FindKingSquare(customFen, 'b');

		// Assert
		// In the FEN position above, white king is on e1 and black king is on e8
		Assert.NotNull(whiteKingSquare);
		Assert.NotNull(blackKingSquare);
		Assert.Equal("e1", whiteKingSquare);
		Assert.Equal("e8", blackKingSquare);
	}

	[Fact]
	public void GetCurrentFENAsync_WhenValidState_ShouldReturnCorrectFENString() { }

	[Fact]
	public async Task IsSquareAttackedAsync_SimplePawnAttack_ReturnsTrue()
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
}
