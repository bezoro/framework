using System.Diagnostics;
using Bezoro.UCI.Domain;

namespace Bezoro.UCI.Tests.Unit.Domain;

public class UciTestsBase : IAsyncLifetime
{
	public const string    STOCKFISH_PATH = "Engine/stockfish/stockfish-windows-x86-64-avx2.exe";
	internal     UciEngine Engine         = null!;

	public virtual Task InitializeAsync()
	{
		if (string.IsNullOrWhiteSpace(STOCKFISH_PATH))
			throw new ArgumentException("Engine path must be provided.", nameof(STOCKFISH_PATH));

		Process engineProcess = new()
		{
			StartInfo = new()
			{
				FileName               = STOCKFISH_PATH,
				RedirectStandardInput  = true,
				RedirectStandardOutput = true,
				UseShellExecute        = false,
				CreateNoWindow         = true
			},
			EnableRaisingEvents = true
		};

		Engine = new(engineProcess);
		Engine.Start();
		return Task.CompletedTask;
	}

	public virtual async Task DisposeAsync()
	{
		await Engine.DisposeAsync();
	}
}
