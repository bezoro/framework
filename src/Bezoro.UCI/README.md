# C# UCI Chess Engine Connector

This C# library provides a robust and efficient interface for communicating with chess engines that implement the
Universal Chess Interface (UCI) protocol, including popular engines such as Stockfish, Leela Chess Zero, and others. The
library enables seamless engine process management, UCI command transmission, and asynchronous response parsing. It is
designed as a reusable component suitable for integration into various .NET applications, including Unity-based chess
applications where it serves as the engine communication layer.

## Features

- **Intuitive API:** A clean and straightforward interface for UCI command communication.
- **Asynchronous Architecture:** Leverages `async/await` patterns for non-blocking engine communication and implements
  `IAsyncDisposable` for proper resource lifecycle management.
- **Reliable Process Management:** Handles chess engine process initialization and termination with robust error
  handling.
- **Engine Discovery:** Automatically retrieves and parses engine identification information (name, author) and
  available UCI configuration options during startup.
- **Real-time Analysis:** Provides an `InfoReceived` event subscription model for live engine search updates, including
  evaluation depth, positional scores, and principal variation lines.
- **Flexible Position Management:** Supports board position configuration via FEN notation or standard starting
  position, with move sequence application capabilities.
- **Comprehensive Search Parameters:** Enables search initiation with configurable time controls, fixed depth analysis,
  or unlimited search modes.
- **Engine Strength Configuration:** Simplifies engine difficulty adjustment through standard UCI options such as
  `Skill Level` and Elo-based limitations (`UCI_LimitStrength` and `UCI_Elo`).
- **Input Validation:** Incorporates built-in validation for FEN strings and UCI move notation to ensure protocol
  compliance.
- **Thread-Safe Design:** Architected for safe operation in multi-threaded environments with proper synchronization.
- **Wide Compatibility:** Targets .NET Standard 2.1 with C# 9.0 language features, ensuring broad compatibility across
  .NET ecosystem implementations.

## Quick Start

Before you begin, ensure you have a UCI-compliant chess engine like [Stockfish](https://stockfishchess.org/download/) downloaded and know the path to its executable.

The following example demonstrates basic `UCIConnector` usage for engine initialization, position analysis, and real-time search monitoring:

```csharp
using Bezoro.UCI;
using System;
using System.Threading.Tasks;

public class Program
{
    public static async Task Main(string[] args)
    {
        // IMPORTANT: Replace with the actual path to your UCI engine executable.
        // For example: "C:/engines/stockfish_15_x64_avx2.exe" on Windows,
        // or "/home/user/stockfish" on Linux.
        var enginePath = "path/to/your/engine";

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