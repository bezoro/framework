# C# UCI Chess Engine Connector

This C# library provides a simple and effective way to communicate with any chess engine that follows the Universal
Chess Interface (UCI) protocol, such as Stockfish, Leela Chess Zero, and others.
It allows you to start an engine process, send UCI commands, and parse the engine's responses asynchronously.

## Features

- **Easy to Use:** A straightforward API for sending and receiving UCI commands.
- **Asynchronous:** Uses `async/await` for non-blocking communication with the engine process.
- **Process Management:** Handles starting and stopping the chess engine process.
- **Compatibility:** Works with any UCI-compliant chess engine.
- **.NET Standard 2.1:** Can be used in a wide range of .NET applications.

## Getting Started

Here is a basic example of how to interact with a chess engine. First, you'll need to have an engine executable (like
`stockfish.exe`) available.

### Example Usage

``` csharp
// Create a new instance of the UCI connector
var uciConnector = new UciConnector("path/to/your/stockfish.exe");

// Start the engine
await uciConnector.StartEngineAsync();

// Set up a new game
await uciConnector.SendCommandAsync("ucinewgame");

// Set the position using FEN notation
await uciConnector.SendCommandAsync("position fen rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");

// Ask the engine to find the best move with a 2-second thinking time
Console.WriteLine("Thinking...");
string bestMove = await uciConnector.GetBestMoveAsync("go movetime 2000");

Console.WriteLine($"The best move is: {bestMove}");

// Stop the engine
uciConnector.StopEngine();
```
