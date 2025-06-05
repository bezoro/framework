using Bezoro.Core.Chess.Common.Helpers;
using NUnit.Framework;

namespace Bezoro.Core.Tests.Chess
{
	[TestFixture]
	public class FenValidatorsTests
	{
	#region Test Methods

		[TestCase("-",   ExpectedResult = true,  TestName = "NoSquare_Dash")]
		[TestCase("e3",  ExpectedResult = true,  TestName = "LegalSquare_e3")]
		[TestCase("a6",  ExpectedResult = true,  TestName = "LegalSquare_a6")]
		[TestCase("i3",  ExpectedResult = false, TestName = "IllegalFile_i3")]
		[TestCase("e4",  ExpectedResult = false, TestName = "IllegalRank_e4")]
		[TestCase("e",   ExpectedResult = false, TestName = "WrongLength_Short")]
		[TestCase("e33", ExpectedResult = false, TestName = "WrongLength_Long")]
		public bool ParseEnPassant_WhenValidInput_ReturnsExpectedResult(string token)
		{
			var result = FenValidators.TryParseEnPassant(token, out var square);

			// For successful parses, the out parameter should echo the token
			if (result)
				Assert.That(square, Is.EqualTo(token));

			return result;
		}

		[TestCase("8/8/8")]                                       // too few ranks
		[TestCase("9/8/8/8/8/8/8/8")]                             // rank > 8 files
		[TestCase("rnbqkbn!/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR")] // invalid char
		public void ValidatePiecePlacement_WhenInvalidInput_ReturnsFalse(string field) =>
			Assert.That(FenValidators.IsValidPiecePlacement(field), Is.False);

		[TestCase("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR")]
		[TestCase("8/8/8/8/8/8/8/8")]
		public void ValidatePiecePlacement_WhenValidInput_ReturnsTrue(string field) =>
			Assert.That(FenValidators.IsValidPiecePlacement(field), Is.True);

	#endregion
	}
}
