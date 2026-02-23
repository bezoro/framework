using Bezoro.UCI.Domain;
using Bezoro.UCI.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientIsReadyTests
{
	[Fact]
	public async Task IsReadyAsync_WhenCancelled_ShouldThrowOperationCanceledException()
	{
		// Arrange
		var (_, client) = UciEngineClientTestHelpers.CreateClientWithTransport();
		var cts = new CancellationTokenSource(TestConstants.CancellationTimeout);

		// Act + Assert
		await FluentActions
			  .Awaiting(() => client.IsReadyAsync(cts.Token))
			  .Should()
			  .ThrowAsync<OperationCanceledException>("operation should be cancelled");
	}
}
