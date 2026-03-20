namespace Bezoro.Chess.UCI.Domain.Common.Exceptions;

/// <summary>
///     Exception thrown when a character does not represent a valid chess piece.
/// </summary>
/// <param name="pieceChar">The invalid piece character.</param>
internal sealed class InvalidPieceCharException(char pieceChar) : Exception($"Invalid piece character: {pieceChar}");
