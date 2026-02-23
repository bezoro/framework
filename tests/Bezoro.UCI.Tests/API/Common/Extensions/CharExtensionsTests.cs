using Bezoro.UCI.API.Common.Enums;
using Bezoro.UCI.API.Common.Extensions;
using Bezoro.UCI.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit.Abstractions;

namespace Bezoro.UCI.Tests.API.Common.Extensions;

[TestSubject(typeof(CharExtensions))]
public class CharExtensionsTests(ITestOutputHelper output) : UnitTestBase(output)
{
	[Theory]
	[InlineData('B', true)]
	[InlineData('b', true)]
	[InlineData('P', false)]
	[InlineData('p', false)]
	public void IsBishop_WhenCalled_ShouldReturnCorrectResult(char pieceChar, bool expected)
	{
		Log("Testing IsBishop with char: {0}", pieceChar);
		bool result = pieceChar.IsBishop();

		result.Should().Be(expected);
	}

	[Theory]
	[InlineData('P', false)]
	[InlineData('N', false)]
	[InlineData('B', false)]
	[InlineData('R', false)]
	[InlineData('Q', false)]
	[InlineData('K', false)]
	[InlineData('p', true)]
	[InlineData('n', true)]
	[InlineData('b', true)]
	[InlineData('r', true)]
	[InlineData('q', true)]
	[InlineData('k', true)]
	public void IsBlack_WhenCalled_ShouldReturnCorrectResult(char pieceChar, bool expected)
	{
		Log("Testing IsBlack with char: {0}", pieceChar);
		bool result = pieceChar.IsBlack();

		result.Should().Be(expected);
	}

	[Theory]
	[InlineData('K', true)]
	[InlineData('k', true)]
	[InlineData('P', false)]
	[InlineData('p', false)]
	public void IsKing_WhenCalled_ShouldReturnCorrectResult(char pieceChar, bool expected)
	{
		Log("Testing IsKing with char: {0}", pieceChar);
		bool result = pieceChar.IsKing();

		result.Should().Be(expected);
	}

	[Theory]
	[InlineData('N', true)]
	[InlineData('n', true)]
	[InlineData('P', false)]
	[InlineData('p', false)]
	public void IsKnight_WhenCalled_ShouldReturnCorrectResult(char pieceChar, bool expected)
	{
		Log("Testing IsKnight with char: {0}", pieceChar);
		bool result = pieceChar.IsKnight();

		result.Should().Be(expected);
	}

	[Theory]
	[InlineData('P', true)]
	[InlineData('p', true)]
	[InlineData('N', false)]
	[InlineData('n', false)]
	public void IsPawn_WhenCalled_ShouldReturnCorrectResult(char pieceChar, bool expected)
	{
		Log("Testing IsPawn with char: {0}", pieceChar);
		bool result = pieceChar.IsPawn();

		result.Should().Be(expected);
	}

	[Theory]
	[InlineData('Q', true)]
	[InlineData('q', true)]
	[InlineData('P', false)]
	[InlineData('p', false)]
	public void IsQueen_WhenCalled_ShouldReturnCorrectResult(char pieceChar, bool expected)
	{
		Log("Testing IsQueen with char: {0}", pieceChar);
		bool result = pieceChar.IsQueen();

		result.Should().Be(expected);
	}

	[Theory]
	[InlineData('R', true)]
	[InlineData('r', true)]
	[InlineData('P', false)]
	[InlineData('p', false)]
	public void IsRook_WhenCalled_ShouldReturnCorrectResult(char pieceChar, bool expected)
	{
		Log("Testing IsRook with char: {0}", pieceChar);
		bool result = pieceChar.IsRook();

		result.Should().Be(expected);
	}

	[Theory]
	[InlineData('X')]
	[InlineData('1')]
	[InlineData(' ')]
	[InlineData('!')]
	public void IsValidPieceChar_WhenInvalid_ShouldReturnFalse(char pieceChar)
	{
		Log("Testing IsValidPieceChar with invalid char: {0}", pieceChar);
		bool result = pieceChar.IsValidPieceChar();

		result.Should().BeFalse();
	}

	[Theory]
	[InlineData('P')]
	[InlineData('N')]
	[InlineData('B')]
	[InlineData('R')]
	[InlineData('Q')]
	[InlineData('K')]
	[InlineData('p')]
	[InlineData('n')]
	[InlineData('b')]
	[InlineData('r')]
	[InlineData('q')]
	[InlineData('k')]
	public void IsValidPieceChar_WhenValid_ShouldReturnTrue(char pieceChar)
	{
		Log("Testing IsValidPieceChar with valid char: {0}", pieceChar);
		bool result = pieceChar.IsValidPieceChar();

		result.Should().BeTrue();
	}

	[Theory]
	[InlineData('P', true)]
	[InlineData('N', true)]
	[InlineData('B', true)]
	[InlineData('R', true)]
	[InlineData('Q', true)]
	[InlineData('K', true)]
	[InlineData('p', false)]
	[InlineData('n', false)]
	[InlineData('b', false)]
	[InlineData('r', false)]
	[InlineData('q', false)]
	[InlineData('k', false)]
	public void IsWhite_WhenCalled_ShouldReturnCorrectResult(char pieceChar, bool expected)
	{
		Log("Testing IsWhite with char: {0}", pieceChar);
		bool result = pieceChar.IsWhite();

		result.Should().Be(expected);
	}

	[Theory]
	[InlineData('X')]
	[InlineData('1')]
	[InlineData(' ')]
	[InlineData('!')]
	public void ThrowIfNotPieceChar_WhenInvalid_ShouldThrow(char pieceChar)
	{
		Log("Testing ThrowIfNotPieceChar with invalid char: {0}", pieceChar);
		var act = () => pieceChar.ThrowIfNotPieceChar();

		act.Should().Throw<Exception>();
	}

	[Theory]
	[InlineData('P', PieceType.Pawn)]
	[InlineData('N', PieceType.Knight)]
	[InlineData('B', PieceType.Bishop)]
	[InlineData('R', PieceType.Rook)]
	[InlineData('Q', PieceType.Queen)]
	[InlineData('K', PieceType.King)]
	[InlineData('p', PieceType.Pawn)]
	[InlineData('n', PieceType.Knight)]
	[InlineData('b', PieceType.Bishop)]
	[InlineData('r', PieceType.Rook)]
	[InlineData('q', PieceType.Queen)]
	[InlineData('k', PieceType.King)]
	public void ToPieceType_WhenCalled_ShouldReturnCorrectPieceType(char pieceChar, PieceType expected)
	{
		Log("Testing ToPieceType with char: {0}", pieceChar);
		var result = pieceChar.ToPieceType();

		result.Should().Be(expected);
	}
}
