using Bezoro.Chess.API.Shared.Enums;
using Bezoro.Chess.Domain.Extensions;
using JetBrains.Annotations;
using SharedCastlingRights = Bezoro.Chess.Domain.Shared.Enums.CastlingRights;

namespace Bezoro.Chess.Tests.Domain.Unit;

[TestSubject(typeof(CastlingRightsMapping))]
public sealed class CastlingRightsMappingTests
{
	[Fact]
	public void ToAPI_WhenCombiningUnlabelledFlags_PreservesAllBits()
	{
		// Arrange
		var mixedRights = SharedCastlingRights.WhiteKingside | SharedCastlingRights.BlackQueenside;

		// Act
		var result = mixedRights.ToAPI();

		// Assert
		Assert.Equal(CastlingRights.WhiteKingside | CastlingRights.BlackQueenside, result);
	}

	[Fact]
	public void ToDomain_WhenCombiningUnlabelledFlags_PreservesAllBits()
	{
		// Arrange
		var mixedRights = CastlingRights.WhiteQueenside | CastlingRights.BlackKingside;

		// Act
		var result = mixedRights.ToDomain();

		// Assert
		Assert.Equal(
			SharedCastlingRights.WhiteQueenside | SharedCastlingRights.BlackKingside,
			result);
	}
}

