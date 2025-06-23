# C# UCI Chess Engine Connector

This C# library provides a simple and effective way to communicate with any chess engine that follows the Universal
Chess Interface (UCI) protocol, such as Stockfish, Leela Chess Zero, and others. It allows you to start an engine
process, send UCI commands, and parse the engine's responses asynchronously. This is a personal implementation that will
be used as a DLL imported into Unity, where Unity will function as a view layer.

## Features

- **Easy to Use:** A straightforward API for sending and receiving UCI commands.
- **Fully Asynchronous:** Uses `async/await` for non-blocking communication with the engine process and supports
  `IAsyncDisposable` for proper resource management.
- **Process Management:** Reliably handles starting and stopping the chess engine process.
- **Engine Introspection:** Automatically parses engine identification info (like name and author) and supported UCI
  options upon startup.
- **Real-time Search Analysis:** Subscribe to an `InfoReceived` event to get live updates from the engine's search,
  including depth, score, and principal variation.
- **Flexible Position Setup:** Set board positions using FEN strings or "startpos," and apply a sequence of subsequent
  moves.
- **Comprehensive Search Control:** Initiate searches with various parameters, including time controls, fixed depth, or
  infinite analysis.
- **Engine Difficulty Control:** Easily adjust the engine's playing strength using common UCI options like `Skill Level`
  or Elo-based limits (`UCI_LimitStrength` and `UCI_Elo`).
- **Robust Input Validation:** Includes internal helpers to validate FEN strings and UCI moves, ensuring that only
  well-formed commands are sent to the engine.
- **Thread-Safe:** Designed for safe concurrent access in multi-threaded environments.
- **Broad Compatibility:** Targets .NET Standard 2.1 and is written in C# 9.0, allowing it to be used in a wide range of
  .NET applications.

## Quick Start

Here's a simple example of how to use the `UCIConnector` to start an engine, get the best move from the starting
position, and see real-time analysis.

```csharp
using Bezoro.UCI;
using System;
using System.Threading.Tasks;

public class Program
{
    public static async Task Main(string[] args)
    {
        // IMPORTANT: Replace with the actual path to your UCI engine executable.
        var enginePath = "C:/path/to/your/stockfish.exe";

        // The 'await using' statement ensures the connector is properly disposed,
        // which also handles stopping the engine process gracefully.
        await using var connector = new UCIConnector(enginePath);
        
        try
        {
            // Subscribe to the InfoReceived event to see real-time engine analysis
            connector.InfoReceived += (sender, eventArgs) =>
            {
                // Displaying only if the analysis has a score and a principal variation.
                if (eventArgs.ScoreCp.HasValue && eventArgs.PrincipalVariation.Any())
                {
                     Console.WriteLine($"Engine analysis: Depth={eventArgs.Depth}, Score={eventArgs.ScoreCp}, PV={string.Join(" ", eventArgs.PrincipalVariation)}");
                }
            };

            // Start the engine and wait for it to be ready
            await connector.StartEngineAsync();
            Console.WriteLine("Engine started successfully.");

            // Print some information about the engine
            Console.WriteLine("\n--- Engine Info ---");
            foreach (var info in connector.EngineInfo)
            {
                Console.WriteLine(info);
            }
            Console.WriteLine("-------------------\n");

            // Set the board to the standard starting position
            await connector.SetPositionAsync("startpos");
            Console.WriteLine("Position set to startpos.");

            // Ask the engine to find the best move, thinking for 2 seconds
            Console.WriteLine("\nSearching for the best move...");
            var bestMove = await connector.GetBestMoveAsync(TimeSpan.FromSeconds(2));

            Console.WriteLine($"\nBest move found: {bestMove}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
}
```