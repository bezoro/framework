using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

public class UCIConnector : IDisposable
{
	private readonly Process      _engineProcess;
	private          StreamReader _processOutput;
	private          StreamWriter _processInput;

	public UCIConnector(string enginePath)
	{
		if (string.IsNullOrWhiteSpace(enginePath))
		{
			throw new ArgumentException("Engine path cannot be null or whitespace.", nameof(enginePath));
		}

		_engineProcess = new Process
		{
			StartInfo = new ProcessStartInfo
			{
				FileName               = enginePath,
				UseShellExecute        = false,
				RedirectStandardInput  = true,
				RedirectStandardOutput = true,
				CreateNoWindow         = true
			}
		};
	}

	/// <summary>
	///     Extracts the active player color from a FEN string.
	/// </summary>
	/// <param name="fen">The FEN string to parse.</param>
	/// <returns>The active player color ('w' for white, 'b' for black), or null if invalid FEN.</returns>
	public static char? GetPlayerColorFromFen(string fen)
	{
		if (string.IsNullOrWhiteSpace(fen))
		{
			return null;
		}

		// FEN format: "pieces activeColor castling enPassant halfmove fullmove"
		// The active color is the second field (index 1) when split by spaces
		string[] fenParts = fen.Split(' ');

		if (fenParts.Length < 2)
		{
			return null;
		}

		string activeColor = fenParts[1].ToLower();

		if (activeColor == "w")
		{
			return 'w'; // White to move
		}

		if (activeColor == "b")
		{
			return 'b'; // Black to move
		}

		return null; // Invalid active color
	}

	/// <summary>
	///     Makes a move from one square to another using algebraic notation.
	/// </summary>
	/// <param name="from">The source square in algebraic notation (e.g., "a1", "e4").</param>
	/// <param name="to">The destination square in algebraic notation (e.g., "a2", "e5").</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="ArgumentException">Thrown when from or to parameters are invalid.</exception>
	public async Task MakeMove(string from, string to)
	{
		if (string.IsNullOrWhiteSpace(from))
		{
			throw new ArgumentException("From square cannot be null or whitespace.", nameof(from));
		}

		if (string.IsNullOrWhiteSpace(to))
		{
			throw new ArgumentException("To square cannot be null or whitespace.", nameof(to));
		}

		// Validate algebraic notation format (basic validation)
		if (!IsValidAlgebraicNotation(from))
		{
			throw new ArgumentException($"Invalid algebraic notation for from square: {from}", nameof(from));
		}

		if (!IsValidAlgebraicNotation(to))
		{
			throw new ArgumentException($"Invalid algebraic notation for to square: {to}", nameof(to));
		}

		// Create the move string in UCI format (from + to)
		string move = from.ToLower() + to.ToLower();

		// Send the position command with the move
		await SendCommand($"position startpos moves {move}");
	}

	public async Task SendCommand(string command)
	{
		if (_processInput == null)
		{
			throw new InvalidOperationException("The engine process has not been started.");
		}

		await _processInput.WriteLineAsync(command);
	}

	/// <summary>
	///     Sets the current game state using a FEN string.
	/// </summary>
	/// <param name="fen">The FEN string representing the desired game state.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="ArgumentException">Thrown when the FEN string is null or empty.</exception>
	public async Task SetGameStateFromFen(string fen)
	{
		if (string.IsNullOrWhiteSpace(fen))
		{
			throw new ArgumentException("FEN string cannot be null or whitespace.", nameof(fen));
		}

		// The UCI protocol uses the "position fen" command to set a position
		await SendCommand($"position fen {fen}");
	}

	public async Task StartEngine()
	{
		_engineProcess.Start();
		_processInput  = _engineProcess.StandardInput;
		_processOutput = _engineProcess.StandardOutput;

		await SendCommand("uci");

		string line;
		var    uciOkReceived = false;
		while ((line = await _processOutput.ReadLineAsync()) != null)
		{
			if (line == "uciok")
			{
				uciOkReceived = true;
				break;
			}
		}

		if (!uciOkReceived)
		{
			throw new Exception("UCI engine did not respond with 'uciok'.");
		}
	}

	/// <summary>
	///     Checks if it's black's turn to move.
	/// </summary>
	/// <returns>True if it's black's turn, false if it's white's turn, null if unable to determine.</returns>
	public async Task<bool?> IsBlackToMove()
	{
		char? currentPlayer = await GetCurrentPlayerColor();
		return currentPlayer == 'b' ? true : currentPlayer == 'w' ? false : null;
	}

	/// <summary>
	///     Checks if it's white's turn to move.
	/// </summary>
	/// <returns>True if it's white's turn, false if it's black's turn, null if unable to determine.</returns>
	public async Task<bool?> IsWhiteToMove()
	{
		char? currentPlayer = await GetCurrentPlayerColor();
		return currentPlayer == 'w' ? true : currentPlayer == 'b' ? false : null;
	}

	/// <summary>
	///     Gets the current player's color from the chess position.
	/// </summary>
	/// <returns>The current player's color ('w' for white, 'b' for black), or null if unable to determine.</returns>
	public async Task<char?> GetCurrentPlayerColor()
	{
		string fen = await GetFen();

		if (string.IsNullOrWhiteSpace(fen))
		{
			return null;
		}

		return GetPlayerColorFromFen(fen);
	}

	public async Task<string> GetBestMove(string command)
	{
		await SendCommand(command);

		string bestMove = null;
		string line;
		while ((line = await _processOutput.ReadLineAsync()) != null)
		{
			if (line.StartsWith("bestmove"))
			{
				string[]? parts = line.Split(' ');
				if (parts.Length > 1)
				{
					bestMove = parts[1];
				}

				break;
			}
		}

		return bestMove;
	}

	/// <summary>
	///     Asks the engine for the current board state and returns the FEN string.
	/// </summary>
	/// <returns>The FEN string representing the current game state.</returns>
	public async Task<string> GetFen()
	{
		await SendCommand("d");
		string line;
		while ((line = await _processOutput.ReadLineAsync()) != null)
		{
			if (line.StartsWith("Fen: "))
			{
				return line.Substring(5);
			}

			// The "d" command output is terminated by a line showing the board's checksum.
			// If we see this, it means we've missed the Fen line and should stop reading.
			if (line.StartsWith("Checkers: "))
			{
				break;
			}
		}

		return null; // Return null if the FEN string couldn't be found.
	}

	public void Dispose()
	{
		StopEngine();
		_processInput?.Dispose();
		_processOutput?.Dispose();
		_engineProcess?.Dispose();
		GC.SuppressFinalize(this);
	}

	public void StopEngine()
	{
		if (_engineProcess != null && !_engineProcess.HasExited)
		{
			SendCommand("quit").Wait();
			_engineProcess.WaitForExit();
		}
	}

	/// <summary>
	///     Validates if a string is in proper algebraic notation format.
	/// </summary>
	/// <param name="square">The square notation to validate.</param>
	/// <returns>True if valid, false otherwise.</returns>
	private static bool IsValidAlgebraicNotation(string square)
	{
		if (string.IsNullOrWhiteSpace(square) || square.Length != 2)
		{
			return false;
		}

		char file = char.ToLower(square[0]);
		char rank = square[1];

		return file >= 'a' && file <= 'h' && rank >= '1' && rank <= '8';
	}
}
