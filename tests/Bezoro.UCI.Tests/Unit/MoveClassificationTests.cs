using Bezoro.UCI.API;
using Bezoro.UCI.API.Types;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Unit;

[TestSubject(typeof(MoveClassification))]
public class MoveClassificationTests : IAsyncLifetime
{
	private const string        StockfishPath = "Engine/stockfish/stockfish-windows-x86-64-avx2.exe";
	private       UCIConnector? _connector;

	public async Task InitializeAsync()
	{
		_connector = new UCIConnector(StockfishPath);
		await _connector.Value.StartEngineAsync();
	}

	public async Task DisposeAsync()
	{
		if (_connector != null)
		{
			await _connector.Value.DisposeAsync();
		}
	}

	[Fact]
	public async Task GetAllLegalMovesWithDetailsAsync_WhenValidState_ShouldReturnValidMoves()
	{
		// Arrange
		// Set the board to the standard starting position.
		await _connector!.SetPositionAsync();

		// Act
		// Retrieve all legal moves along with their detailed classifications.
		List<MoveClassification> moves = await _connector.GetAllLegalMovesWithDetailsAsync();

		// Assert
		// There should be 20 legal moves from the starting position.
		Assert.NotNull(moves);
		Assert.Equal(20, moves.Count);

		// For the starting position, none of the moves are special (captures, castling, etc.).
		Assert.All(moves, move =>
		{
			Assert.False(move.IsCapture, $"Move '{move.Move}' should not be a capture from the start position.");
			Assert.False(move.IsCastling, $"Move '{move.Move}' should not be a castling move from the start position.");
			Assert.False(move.IsPromotion, $"Move '{move.Move}' should not be a promotion from the start position.");
			Assert.False(move.IsEnPassant,
				$"Move '{move.Move}' should not be an en passant capture from the start position.");
		});

		// Verify that a common opening move like 'e2e4' is in the list.
		Assert.Contains(moves, m => m.Move == "e2e4");
	}

	[Theory]
	// Test case for a normal pawn move from the starting position.
	[InlineData("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1", "e2e4", false, false, false, false)]
	// Test case for a capture. White pawn on e4 captures a black pawn on d5.
	[InlineData("rnbqkbnr/ppp1pppp/8/3p4/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2", "e4d5", true, false, false, false)]
	// Test case for Kingside castling.
	[InlineData("r3k2r/pppp1ppp/8/8/8/8/PPPP1PPP/R3K2R w KQkq - 0 1", "e1g1", false, true, false, false)]
	// Test case for Queenside castling.
	[InlineData("r3k2r/pppp1ppp/8/8/8/8/PPPP1PPP/R3K2R w KQkq - 0 1", "e1c1", false, true, false, false)]
	// Test case for a pawn promoting to a queen.
	[InlineData("4k3/P7/8/8/8/8/8/4K3 w - - 0 1", "a7a8q", false, false, true, false)]
	// Test case for an En Passant capture, which is both a capture and en passant.
	[InlineData("rnbqkbnr/ppp1p1pp/8/3pPp2/8/8/PPPP1PPP/RNBQKBNR w KQkq f6 0 3", "e5f6", true, false, false, true)]
	public async Task GetAllLegalMovesWithDetailsAsync_WithVariousBoardStates_CorrectlyClassifiesMoves(
		string fen, string uciMove, bool isCapture, bool isCastling, bool isPromotion, bool isEnPassant)
	{
		// Arrange
		// Set the custom board position using the provided FEN string.
		await _connector!.SetPositionAsync(fen);

		// Act
		// Get all legal moves with their detailed information.
		// This assumes the returned object has a 'Classification' property of the type you provided.
		List<MoveClassification> movesWithDetails = await _connector.GetAllLegalMovesWithDetailsAsync();

		// Assert
		// Find the specific move we're testing for in the list of legal moves.
		MoveClassification? specificMove = movesWithDetails.FirstOrDefault(m => m.Move == uciMove);

		// Verify that the move was found and its classification properties are correct.
		Assert.NotNull(specificMove);
		Assert.Equal(isCapture,   specificMove.Value.IsCapture);
		Assert.Equal(isCastling,  specificMove.Value.IsCastling);
		Assert.Equal(isPromotion, specificMove.Value.IsPromotion);
		Assert.Equal(isEnPassant, specificMove.Value.IsEnPassant);
	}
}
