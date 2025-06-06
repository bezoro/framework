using Bezoro.Chess.Chess.Board;
using Bezoro.Chess.Chess.Common.Helpers;

namespace Bezoro.Chess.UnitTests;

[TestFixture]
[TestOf(typeof(AlgebraicNotationUtils))]
public class AlgebraicNotationUtilsTests
{
#region Test Methods

	[TestCase("a",       TestName = "FromAlgebraic_TooShort_a_ThrowsArgumentException")]
	[TestCase("1",       TestName = "FromAlgebraic_TooShort_1_ThrowsArgumentException")]
	[TestCase("1e",      TestName = "FromAlgebraic_InvalidFile_1e_ThrowsArgumentException")]
	[TestCase("@1",      TestName = "FromAlgebraic_InvalidFile_SpecialChar_ThrowsArgumentException")]
	[TestCase("aa",      TestName = "FromAlgebraic_InvalidRank_aa_ThrowsArgumentException")]
	[TestCase("e0",      TestName = "FromAlgebraic_InvalidRank_e0_ThrowsArgumentException")]
	[TestCase("a0",      TestName = "FromAlgebraic_InvalidRank_a0_ThrowsArgumentException")]
	[TestCase("a-1",     TestName = "FromAlgebraic_InvalidRank_a_negative1_ThrowsArgumentException")]
	[TestCase("e",       TestName = "FromAlgebraic_InvalidRank_e_ThrowsArgumentException")]
	[TestCase("a1extra", TestName = "FromAlgebraic_InvalidRank_a1extra_ThrowsArgumentException")]
	[TestCase("a1a",     TestName = "FromAlgebraic_InvalidRank_a1a_ThrowsArgumentException")]
	public void FromAlgebraic_InvalidFormatOrContent_ThrowsArgumentException(string algebraic)
	{
		// Act & Assert
		var ex = Assert.Throws<ArgumentException>(() => AlgebraicNotationUtils.FromAlgebraic(algebraic));
		Assert.That(ex.ParamName, Is.EqualTo("algebraicSquare"));
	}

	[TestCase("a1")]
	[TestCase("h8")]
	[TestCase("e4")]
	public void FromAlgebraic_ToAlgebraic_RoundTrip(string initialAlgebraic)
	{
		// Act
		var position       = AlgebraicNotationUtils.FromAlgebraic(initialAlgebraic);
		var finalAlgebraic = AlgebraicNotationUtils.ToAlgebraic(position);

		// Assert
		Assert.That(finalAlgebraic, Is.EqualTo(initialAlgebraic.ToLowerInvariant()));
	}

	[TestCase("a1", 0, 0, TestName = "FromAlgebraic_a1_Returns_0_0")]
	[TestCase("h8", 7, 7, TestName = "FromAlgebraic_h8_Returns_7_7")]
	[TestCase("e4", 4, 3, TestName = "FromAlgebraic_e4_Returns_4_3")]
	[TestCase("A1", 0, 0, TestName = "FromAlgebraic_A1_CaseInsensitive_Returns_0_0")]
	[TestCase("H8", 7, 7, TestName = "FromAlgebraic_H8_CaseInsensitive_Returns_7_7")]
	public void FromAlgebraic_ValidCases_ReturnsExpectedPosition(
		string algebraic,
		int expectedFile,
		int expectedRank)
	{
		// Act
		var position = AlgebraicNotationUtils.FromAlgebraic(algebraic);

		// Assert
		Assert.That(position.Column, Is.EqualTo(expectedFile));
		Assert.That(position.Rank,   Is.EqualTo(expectedRank));
	}

	[TestCase("",  TestName = "FromAlgebraic_EmptyInput_ThrowsArgumentNullException")]
	[TestCase(" ", TestName = "FromAlgebraic_WhitespaceInput_ThrowsArgumentNullException")]
	public void FromAlgebraic_WhitespaceInput_ThrowsArgumentNullException(string input)
	{
		// Act & Assert
		var ex = Assert.Throws<ArgumentNullException>(() => AlgebraicNotationUtils.FromAlgebraic(input));
		Assert.That(ex.ParamName, Is.EqualTo("algebraicSquare"));
	}

	[TestCase(-1, 0, TestName = "ToAlgebraic_FileRank_NegativeFile_ThrowsArgumentOutOfRangeException")]
	[TestCase(26, 0, TestName = "ToAlgebraic_FileRank_FileTooLarge_ThrowsArgumentOutOfRangeException")]
	public void ToAlgebraic_FileRank_InvalidFile_ThrowsArgumentOutOfRangeException(int fileIndex, int rankIndex)
	{
		// Act & Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(
			() => AlgebraicNotationUtils.ToAlgebraic(fileIndex, rankIndex));

		Assert.That(ex.ParamName, Is.EqualTo("fileIndex"));
	}

	[TestCase(0, -1, TestName = "ToAlgebraic_FileRank_NegativeRank_ThrowsArgumentOutOfRangeException")]
	public void ToAlgebraic_FileRank_InvalidRank_ThrowsArgumentOutOfRangeException(int fileIndex, int rankIndex)
	{
		// Act & Assert
		var ex = Assert.Throws<ArgumentOutOfRangeException>(
			() => AlgebraicNotationUtils.ToAlgebraic(fileIndex, rankIndex));

		Assert.That(ex.ParamName, Is.EqualTo("rankIndex"));
	}

	[TestCase(0,  0,  "a1",   TestName = "ToAlgebraic_FileRank_0_0_Returns_a1")]
	[TestCase(7,  7,  "h8",   TestName = "ToAlgebraic_FileRank_7_7_Returns_h8")]
	[TestCase(4,  3,  "e4",   TestName = "ToAlgebraic_FileRank_4_3_Returns_e4")]
	[TestCase(25, 0,  "z1",   TestName = "ToAlgebraic_FileRank_25_0_ExtendedFile_Returns_z1")]
	[TestCase(0,  25, "a26",  TestName = "ToAlgebraic_FileRank_0_25_ExtendedRank_Returns_a26")]
	[TestCase(0,  99, "a100", TestName = "ToAlgebraic_FileRank_0_99_LargeRank_Returns_a100")] // Rank 100
	public void ToAlgebraic_FileRank_ValidCases_ReturnsExpectedNotation(
		int fileIndex,
		int rankIndex,
		string expectedNotation)
	{
		// Act
		var notation = AlgebraicNotationUtils.ToAlgebraic(fileIndex, rankIndex);

		// Assert
		Assert.That(notation, Is.EqualTo(expectedNotation));
	}

	[TestCase(0,  0)]
	[TestCase(7,  7)]
	[TestCase(4,  3)]
	[TestCase(25, 25)]
	public void ToAlgebraic_Overloads_ProduceSameResult(int file, int rank)
	{
		// Arrange
		var position = new BoardPosition(file, rank);

		// Act
		var notationFromPosition = AlgebraicNotationUtils.ToAlgebraic(position);
		var notationFromFileRank = AlgebraicNotationUtils.ToAlgebraic(file, rank);

		// Assert
		Assert.That(notationFromPosition, Is.EqualTo(notationFromFileRank));
	}

#region ToAlgebraic(BoardPosition) Tests

	[TestCase(0,  0,  "a1",  TestName = "ToAlgebraic_Position_0_0_Returns_a1")]
	[TestCase(7,  7,  "h8",  TestName = "ToAlgebraic_Position_7_7_Returns_h8")]
	[TestCase(4,  3,  "e4",  TestName = "ToAlgebraic_Position_4_3_Returns_e4")]
	[TestCase(25, 0,  "z1",  TestName = "ToAlgebraic_Position_25_0_Returns_z1")]
	[TestCase(0,  25, "a26", TestName = "ToAlgebraic_Position_0_25_Returns_a26")]
	public void ToAlgebraic_Position_ValidCases_ReturnsExpectedNotation(int file, int rank, string expectedNotation)
	{
		// Arrange
		var position = new BoardPosition(file, rank);

		// Act
		var notation = AlgebraicNotationUtils.ToAlgebraic(position);

		// Assert
		Assert.That(notation, Is.EqualTo(expectedNotation));
	}

#endregion

#endregion
}
