using Bezoro.Chess.Board.Models;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Common.Helpers;
using Bezoro.Chess.Pieces.Models;

namespace Bezoro.Chess.UnitTests.Common.Helpers;

[TestFixture]
[TestOf(typeof(FenUtils))]
public class FenUtilsUnitTests
{
#region Test Methods

	[Test]
	public void GenerateFEN_CustomPosition_ReturnsCorrectFEN()
	{
		// Create a board with a custom position
		var board = FenUtils.Parse("r1bqkb1r/pppp1ppp/2n2n2/4p3/4P3/2N2N2/PPPP1PPP/R1BQKB1R w KQkq - 4 4");

		// Just check the piece placement part (first segment before the space)
		var fenPiecePlacement = board.PiecePlacement;

		Assert.That(fenPiecePlacement, Is.EqualTo("r1bqkb1r/pppp1ppp/2n2n2/4p3/4P3/2N2N2/PPPP1PPP/R1BQKB1R"));
	}

	[Test]
	public void Parse_WhenStartBoard_ParsesCorrectly()
	{
		var fenData = FenUtils.Parse(FenUtils.START_FEN);

		Assert.That(fenData.FullString, Is.EquivalentTo("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR White All - 0 1"));
	}

	[Test]
	public void ParseFEN_EmptyBoard_CreatesEmptyBoard()
	{
		// Empty board FEN
		var emptyBoardFEN = "8/8/8/8/8/8/8/8 w - - 0 1";

		var board = new BoardModel(8, 8, FenUtils.Parse(emptyBoardFEN).PiecePlacement);

		// Verify all squares are empty
		for (var rank = 1 ; rank <= 8 ; rank++)
		{
			for (var file = 'a' ; file <= 'h' ; file++)
			{
				Assert.That(board.GetPieceAt($"{file}{rank}"), Is.Null);
			}
		}
	}

	[Test]
	public void ParseFEN_InitialPosition_CreatesCorrectBoard()
	{
		// Standard initial position
		var initialFEN = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

		var board = new BoardModel(8, 8, FenUtils.Parse(initialFEN).PiecePlacement);

		// Verify white pieces are in correct positions
		Assert.Multiple(
			() =>
			{
				// White pieces - back rank
				Assert.That(
					board.GetPieceAt("a1"), Is.TypeOf<RookModel>().And.Property("Color").EqualTo(PlayerColor.White));

				Assert.That(
					board.GetPieceAt("b1"), Is.TypeOf<KnightModel>().And.Property("Color").EqualTo(PlayerColor.White));

				Assert.That(
					board.GetPieceAt("c1"), Is.TypeOf<BishopModel>().And.Property("Color").EqualTo(PlayerColor.White));

				Assert.That(
					board.GetPieceAt("d1"), Is.TypeOf<QueenModel>().And.Property("Color").EqualTo(PlayerColor.White));

				Assert.That(
					board.GetPieceAt("e1"), Is.TypeOf<KingModel>().And.Property("Color").EqualTo(PlayerColor.White));

				Assert.That(
					board.GetPieceAt("f1"), Is.TypeOf<BishopModel>().And.Property("Color").EqualTo(PlayerColor.White));

				Assert.That(
					board.GetPieceAt("g1"), Is.TypeOf<KnightModel>().And.Property("Color").EqualTo(PlayerColor.White));

				Assert.That(
					board.GetPieceAt("h1"), Is.TypeOf<RookModel>().And.Property("Color").EqualTo(PlayerColor.White));

				// White pawns
				for (var file = 'a' ; file <= 'h' ; file++)
				{
					Assert.That(
						board.GetPieceAt($"{file}2"),
						Is.TypeOf<PawnModel>().And.Property("Color").EqualTo(PlayerColor.White));
				}

				// Black pieces - back rank
				Assert.That(
					board.GetPieceAt("a8"), Is.TypeOf<RookModel>().And.Property("Color").EqualTo(PlayerColor.Black));

				Assert.That(
					board.GetPieceAt("b8"), Is.TypeOf<KnightModel>().And.Property("Color").EqualTo(PlayerColor.Black));

				Assert.That(
					board.GetPieceAt("c8"), Is.TypeOf<BishopModel>().And.Property("Color").EqualTo(PlayerColor.Black));

				Assert.That(
					board.GetPieceAt("d8"), Is.TypeOf<QueenModel>().And.Property("Color").EqualTo(PlayerColor.Black));

				Assert.That(
					board.GetPieceAt("e8"), Is.TypeOf<KingModel>().And.Property("Color").EqualTo(PlayerColor.Black));

				Assert.That(
					board.GetPieceAt("f8"), Is.TypeOf<BishopModel>().And.Property("Color").EqualTo(PlayerColor.Black));

				Assert.That(
					board.GetPieceAt("g8"), Is.TypeOf<KnightModel>().And.Property("Color").EqualTo(PlayerColor.Black));

				Assert.That(
					board.GetPieceAt("h8"), Is.TypeOf<RookModel>().And.Property("Color").EqualTo(PlayerColor.Black));

				// Black pawns
				for (var file = 'a' ; file <= 'h' ; file++)
				{
					Assert.That(
						board.GetPieceAt($"{file}7"),
						Is.TypeOf<PawnModel>().And.Property("Color").EqualTo(PlayerColor.Black));
				}

				// Middle ranks should be empty
				for (var rank = 3 ; rank <= 6 ; rank++)
				{
					for (var file = 'a' ; file <= 'h' ; file++)
					{
						Assert.That(board.GetPieceAt($"{file}{rank}"), Is.Null);
					}
				}
			});
	}

	[Test]
	public void ParseFEN_InvalidFEN_ThrowsException()
	{
		// Invalid FEN strings
		var invalidFENs = new[]
		{
			"too/short",
			"9/8/8/8/8/8/8/8 w - - 0 1",                               // Invalid digit (9)
			"8/8/8/8/8/8/8/8/8 w - - 0 1",                             // Too many ranks
			"8/8/8/8/8/8/8 w - - 0 1",                                 // Too few ranks
			"8/8/8/8/8/8/8/8z w - - 0 1",                              // Invalid character
			"rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNZ w KQkq - 0 1" // Invalid piece
		};

		foreach (var invalidFEN in invalidFENs)
		{
			Assert.Throws<ArgumentException>(() => FenUtils.Parse(invalidFEN));
		}
	}

	[Test]
	public void ParseFEN_MidGamePosition_CreatesCorrectBoard()
	{
		// A typical mid-game position
		var midGameFEN = "r1bqkb1r/pppp1ppp/2n2n2/4p3/4P3/2N2N2/PPPP1PPP/R1BQKB1R w KQkq - 4 4";

		var board = new BoardModel(8, 8, FenUtils.Parse(midGameFEN).PiecePlacement);

		// Verify specific pieces are in correct positions
		Assert.Multiple(
			() =>
			{
				// Check a few white pieces
				Assert.That(
					board.GetPieceAt("e1"), Is.TypeOf<KingModel>().And.Property("Color").EqualTo(PlayerColor.White));

				Assert.That(
					board.GetPieceAt("e4"), Is.TypeOf<PawnModel>().And.Property("Color").EqualTo(PlayerColor.White));

				Assert.That(
					board.GetPieceAt("c3"), Is.TypeOf<KnightModel>().And.Property("Color").EqualTo(PlayerColor.White));

				Assert.That(
					board.GetPieceAt("f3"), Is.TypeOf<KnightModel>().And.Property("Color").EqualTo(PlayerColor.White));

				// Check a few black pieces
				Assert.That(
					board.GetPieceAt("e8"), Is.TypeOf<KingModel>().And.Property("Color").EqualTo(PlayerColor.Black));

				Assert.That(
					board.GetPieceAt("e5"), Is.TypeOf<PawnModel>().And.Property("Color").EqualTo(PlayerColor.Black));

				Assert.That(
					board.GetPieceAt("c6"), Is.TypeOf<KnightModel>().And.Property("Color").EqualTo(PlayerColor.Black));

				Assert.That(
					board.GetPieceAt("f6"), Is.TypeOf<KnightModel>().And.Property("Color").EqualTo(PlayerColor.Black));

				// Check empty squares
				Assert.That(board.GetPieceAt("d3"), Is.Null);
				Assert.That(board.GetPieceAt("e3"), Is.Null);
			});
	}

	[Test]
	public void TryParse_ValidMinimalFen_ReturnsTrue()
	{
		const string fen = "8/8/8/8/8/8/8/8 w - - 0 1";

		var ok = FenUtils.TryParse(fen, out _, out var error);

		Assert.Multiple(
			() =>
			{
				Assert.That(ok,    Is.True);
				Assert.That(error, Is.Null);
			});
	}

	[Test]
	public void TryParseCastling_AllRights_ReturnsCombinedFlags()
	{
		var ok = FenUtils.TryParseCastling("KQkq", out var rights);

		var expected = CastlingRights.WhiteKingSide  |
					   CastlingRights.WhiteQueenSide |
					   CastlingRights.BlackKingSide  |
					   CastlingRights.BlackQueenSide;

		Assert.Multiple(
			() =>
			{
				Assert.That(ok,     Is.True);
				Assert.That(rights, Is.EqualTo(expected));
			});
	}

	[Test]
	public void TryParseCastling_Dash_ReturnsTrueAndNoRights()
	{
		var ok = FenUtils.TryParseCastling("-", out var rights);

		Assert.Multiple(
			() =>
			{
				Assert.That(ok,     Is.True);
				Assert.That(rights, Is.EqualTo(CastlingRights.None));
			});
	}

	[Test]
	public void TryParseCastling_DuplicatesAreAllowed_ReturnsTrue()
	{
		var ok = FenUtils.TryParseCastling("KK", out var rights);

		Assert.Multiple(
			() =>
			{
				Assert.That(ok,     Is.True);
				Assert.That(rights, Is.EqualTo(CastlingRights.WhiteKingSide));
			});
	}

	[TestCase("-",   ExpectedResult = true,  TestName = "NoSquare_Dash")]
	[TestCase("e3",  ExpectedResult = true,  TestName = "LegalSquare_e3")]
	[TestCase("a6",  ExpectedResult = true,  TestName = "LegalSquare_a6")]
	[TestCase("i3",  ExpectedResult = false, TestName = "IllegalFile_i3")]
	[TestCase("e4",  ExpectedResult = false, TestName = "IllegalRank_e4")]
	[TestCase("e",   ExpectedResult = false, TestName = "WrongLength_Short")]
	[TestCase("e33", ExpectedResult = false, TestName = "WrongLength_Long")]
	public bool ParseEnPassant_WhenValidInput_ReturnsExpectedResult(string token)
	{
		var result = FenUtils.TryParseEnPassant(token, out var square);

		// For successful parses, the out parameter should echo the token
		if (result)
			Assert.That(square, Is.EqualTo(token));

		return result;
	}

	[TestCase("8/8/8 w - - 0 1")]            // bad piece placement (too few ranks)
	[TestCase("8/8/8/8/8/8/8/8 x - - 0 1")]  // bad active color
	[TestCase("8/8/8/8/8/8/8/8 w -z - 0 1")] // bad castling string
	[TestCase("8/8/8/8/8/8/8/8 w - z9 0 1")] // bad en-passant
	[TestCase("8/8/8/8/8/8/8/8 w - - -1 1")] // negative half-move clock
	[TestCase("8/8/8/8/8/8/8/8 w - - 0 0")]  // full-move number < 1
	public void TryParse_InvalidFen_ReturnsFalse(string fen)
	{
		var ok = FenUtils.TryParse(fen, out _, out var error);

		Assert.Multiple(
			() =>
			{
				Assert.That(ok,    Is.False);
				Assert.That(error, Is.Not.Null.And.Not.Empty);
			});
	}

	[TestCase("A")]
	[TestCase("KQkqz")]
	[TestCase(" ")]
	public void TryParseCastling_InvalidCharacter_ReturnsFalse(string token)
	{
		var ok = FenUtils.TryParseCastling(token, out _);

		Assert.That(ok, Is.False);
	}

	[TestCase("")]
	[TestCase("W")]
	[TestCase("bb")]
	[TestCase("x")]
	public void TryParseColor_InvalidTokens_ReturnsFalse(string token)
	{
		var ok = FenUtils.TryParseColor(token, out _);

		Assert.That(ok, Is.False);
	}

	[TestCase("w", PlayerColor.White)]
	[TestCase("b", PlayerColor.Black)]
	public void TryParseColor_ValidTokens_ReturnExpectedColor(string token, PlayerColor expected)
	{
		var ok = FenUtils.TryParseColor(token, out var color);

		Assert.Multiple(
			() =>
			{
				Assert.That(ok,    Is.True);
				Assert.That(color, Is.EqualTo(expected));
			});
	}

	[TestCase("8/8/8")]                                       // too few ranks
	[TestCase("9/8/8/8/8/8/8/8")]                             // rank > 8 files
	[TestCase("rnbqkbn!/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR")] // invalid char
	public void ValidatePiecePlacement_WhenInvalidInput_ReturnsFalse(string field) =>
		Assert.That(FenUtils.IsValidPiecePlacement(field), Is.False);

	[TestCase("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR")]
	[TestCase("8/8/8/8/8/8/8/8")]
	public void ValidatePiecePlacement_WhenValidInput_ReturnsTrue(string field) =>
		Assert.That(FenUtils.IsValidPiecePlacement(field), Is.True);

#endregion
}
