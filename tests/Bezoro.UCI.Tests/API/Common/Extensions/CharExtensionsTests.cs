using Bezoro.UCI.API.Common.Enums;
using Bezoro.UCI.API.Common.Extensions;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.API.Common.Extensions;

[TestSubject(typeof(CharExtensions))]
public static class CharExtensionsTests
{
	public class Unit
	{
		[Theory]
		[InlineData('B', true)]
		[InlineData('b', true)]
		[InlineData('P', false)]
		[InlineData('p', false)]
		public void IsBishop_ShouldReturnCorrectType_ForValidPieceChars(char pieceChar, bool expected)
		{
			// Act
			bool result = pieceChar.IsBishop();

			// Assert
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
		public void IsBlack_ShouldReturnCorrectColor_ForValidPieceChars(char pieceChar, bool expected)
		{
			// Act
			bool result = pieceChar.IsBlack();

			// Assert
			result.Should().Be(expected);
		}

		[Theory]
		[InlineData('K', true)]
		[InlineData('k', true)]
		[InlineData('P', false)]
		[InlineData('p', false)]
		public void IsKing_ShouldReturnCorrectType_ForValidPieceChars(char pieceChar, bool expected)
		{
			// Act
			bool result = pieceChar.IsKing();

			// Assert
			result.Should().Be(expected);
		}

		[Theory]
		[InlineData('N', true)]
		[InlineData('n', true)]
		[InlineData('P', false)]
		[InlineData('p', false)]
		public void IsKnight_ShouldReturnCorrectType_ForValidPieceChars(char pieceChar, bool expected)
		{
			// Act
			bool result = pieceChar.IsKnight();

			// Assert
			result.Should().Be(expected);
		}

		[Theory]
		[InlineData('P', true)]
		[InlineData('p', true)]
		[InlineData('N', false)]
		[InlineData('n', false)]
		public void IsPawn_ShouldReturnCorrectType_ForValidPieceChars(char pieceChar, bool expected)
		{
			// Act
			bool result = pieceChar.IsPawn();

			// Assert
			result.Should().Be(expected);
		}

		[Theory]
		[InlineData('Q', true)]
		[InlineData('q', true)]
		[InlineData('P', false)]
		[InlineData('p', false)]
		public void IsQueen_ShouldReturnCorrectType_ForValidPieceChars(char pieceChar, bool expected)
		{
			// Act
			bool result = pieceChar.IsQueen();

			// Assert
			result.Should().Be(expected);
		}

		[Theory]
		[InlineData('R', true)]
		[InlineData('r', true)]
		[InlineData('P', false)]
		[InlineData('p', false)]
		public void IsRook_ShouldReturnCorrectType_ForValidPieceChars(char pieceChar, bool expected)
		{
			// Act
			bool result = pieceChar.IsRook();

			// Assert
			result.Should().Be(expected);
		}

		[Theory]
		[InlineData('X')]
		[InlineData('1')]
		[InlineData(' ')]
		[InlineData('!')]
		public void IsValidPieceChar_ShouldReturnFalse_ForInvalidPieceChars(char pieceChar)
		{
			// Act
			bool result = pieceChar.IsValidPieceChar();

			// Assert
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
		public void IsValidPieceChar_ShouldReturnTrue_ForValidPieceChars(char pieceChar)
		{
			// Act
			bool result = pieceChar.IsValidPieceChar();

			// Assert
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
		public void IsWhite_ShouldReturnCorrectColor_ForValidPieceChars(char pieceChar, bool expected)
		{
			// Act
			bool result = pieceChar.IsWhite();

			// Assert
			result.Should().Be(expected);
		}

		[Theory]
		[InlineData('X')]
		[InlineData('1')]
		[InlineData(' ')]
		[InlineData('!')]
		public void ThrowIfNotPieceChar_ShouldThrow_ForInvalidPieceChars(char pieceChar)
		{
			// Act
			var act = () => pieceChar.ThrowIfNotPieceChar();

			// Assert
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
		public void ToPieceType_ShouldReturnCorrectPieceType_ForValidPieceChars(char pieceChar, PieceType expected)
		{
			// Act
			var result = pieceChar.ToPieceType();

			// Assert
			result.Should().Be(expected);
		}
	}
}
