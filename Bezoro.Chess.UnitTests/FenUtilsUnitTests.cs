using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Helpers;

namespace Bezoro.Chess.UnitTests;

[TestFixture]
public class FenUtilsUnitTests
{
#region Test Methods

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
