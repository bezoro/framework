using Bezoro.UCI.Domain;
using Bezoro.UCI.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientUciInitTests
{
	[Fact]
	public async Task WhenCancelled_ShouldThrowOperationCanceledException()
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
