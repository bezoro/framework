using Bezoro.UCI.Domain;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Unit.Domain;

[TestSubject(typeof(UciEngine))]
public class UciEnginePerfTest : UciTestsBase
{
	[Fact]
	public async Task SendCommandAsync_When100ParallelCommand_HandlesAllCorrectly()
	{
		await InitializeAsync();

		var tasks = Enumerable.Range(0, 100)
							  .Select(_ => Engine.SendCommandAsync("uci", CancellationToken.None));

		var responses = await Task.WhenAll(tasks);

		Assert.Multiple(() =>
		{
			Assert.NotNull(responses);
			Assert.NotEmpty(responses);
			foreach (var response in responses)
			{
				Assert.Contains("uciok", response.Completed.Result);
				Assert.Contains("uciok", response.Lines);
			}
		});

		await DisposeAsync();
	}

	[Fact]
	public async Task SendCommandAsync_When100SequentialCommand_HandlesAllCorrectly()
	{
		await InitializeAsync();

		var tasks = new List<Task<UciEngine.UciCommandResponse>>();
		for (var i = 0; i < 100; i++)
			tasks.Add(Engine.SendCommandAsync("uci", CancellationToken.None));

		var responses = await Task.WhenAll(tasks);

		Assert.Multiple(() =>
		{
			Assert.NotNull(responses);
			Assert.NotEmpty(responses);
			foreach (var response in responses)
			{
				Assert.Contains("uciok", response.Completed.Result);
				Assert.Contains("uciok", response.Lines);
			}
		});

		await DisposeAsync();
	}

	[Fact]
	public async Task WriteLineAsync_When100ParallelCommands_HandlesCorrectly()
	{
		await InitializeAsync();

		var tasks = Enumerable.Range(0, 100)
							  .Select(_ => Engine.WriteLineAsync("uci", CancellationToken.None).AsTask());

		await Task.WhenAll(tasks);

		await DisposeAsync();
	}

	[Fact]
	public async Task WriteLineAsync_When100SequentialCommands_HandlesCorrectly()
	{
		await InitializeAsync();

		for (var i = 0; i < 100; i++) await Engine.WriteLineAsync("uci", CancellationToken.None);

		await DisposeAsync();
	}
}
