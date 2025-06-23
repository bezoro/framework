# C# UCI Chess Engine Connector

This C# library provides a simple and effective way to communicate with any chess engine that follows the Universal Chess Interface (UCI) protocol, such as Stockfish, Leela Chess Zero, and others. It allows you to start an engine process, send UCI commands, and parse the engine's responses asynchronously. This is a personal implementation that will be used as a DLL imported into Unity, where Unity will function as a view layer.

## Features

- **Easy to Use:** A straightforward API for sending and receiving UCI commands.
- **Asynchronous:** Uses `async/await` for non-blocking communication with the engine process.
- **Process Management:** Handles starting and stopping the chess engine process.
- **Compatibility:** Works with any UCI-compliant chess engine.
- **.NET Standard 2.1:** Can be used in a wide range of .NET applications.
- **C# 9.0:** Developed using C# 9.0 features and syntax.

