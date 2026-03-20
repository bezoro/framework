using Bezoro.Chess.UCI.Protocol.API;
using Bezoro.Chess.UCI.Protocol.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientUciInitTests
{
	[Fact]
	public async Task UciInitAsync_WhenCancelled_ShouldThrowOperationCanceledException()
	{
		// Arrange
		var (_, client) = UciEngineClientTestHelpers.CreateClientWithTransport();
		var cts = new CancellationTokenSource(TestConstants.CancellationTimeout);

		// Act + Assert
		await FluentActions
			  .Awaiting(() => client.UciInitAsync(cts.Token))
			  .Should()
			  .ThrowAsync<OperationCanceledException>("operation should be cancelled");
	}
}
