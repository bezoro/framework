using System;
using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Helpers
{
	internal static class FenParser
	{
		private const int BoardSize = 8;

		public static GameState FenToGameState(string fen)
		{
			(string placement, string color, string castling, string enPassant, string halfMove, string fullMove) =
				SplitFenString(fen);

			return new GameState
			{
				Board                 = ParsePiecePlacement(placement),
				ActiveColor           = ParseActiveColor(color),
				Castling              = ParseCastlingRights(castling),
				EnPassantTargetSquare = ParseEnPassantTarget(enPassant),
				HalfMoveClock         = ParseHalfMoveClock(halfMove),
				FullMoveNumber        = ParseFullMoveNumber(fullMove)
			};
		}

		private static (string placement, string color, string castling,
			string enPassant, string halfMove, string fullMove) SplitFenString(string fen)
		{
			string[]? parts = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);

			const int expectedFenPartCount = 6;
			if (parts.Length != expectedFenPartCount)
			{
				throw new ArgumentException(
					$"Invalid FEN string: expected {expectedFenPartCount} fields, found {parts.Length}.", nameof(fen));
			}

			return (parts[0], parts[1], parts[2], parts[3], parts[4], parts[5]);
		}

		private static Board ParsePiecePlacement(string placementPart)
		{
			// Bit containers we will fill while scanning the FEN
			ulong wP = 0, wN = 0, wB = 0, wR = 0, wQ = 0, wK = 0;
			ulong bP = 0, bN = 0, bB = 0, bR = 0, bQ = 0, bK = 0;

			// FEN lists ranks from 8 down to 1.  Bit 0 = a1, so the first square
			// we encounter (a8) is bit 56.
			var rankOffset = 56; // a8
			foreach (string rank in placementPart.Split('/'))
			{
				var file = 0; // a-file
				foreach (char c in rank)
				{
					if (char.IsDigit(c))
					{
						file += c - '0'; // Skip empty squares
						continue;
					}

					int   squareIndex = rankOffset + file; // 0 ≤ index ≤ 63
					ulong bit         = 1UL << squareIndex;

					switch (c)
					{
						case 'P': wP |= bit; break;
						case 'N': wN |= bit; break;
						case 'B': wB |= bit; break;
						case 'R': wR |= bit; break;
						case 'Q': wQ |= bit; break;
						case 'K': wK |= bit; break;

						case 'p': bP |= bit; break;
						case 'n': bN |= bit; break;
						case 'b': bB |= bit; break;
						case 'r': bR |= bit; break;
						case 'q': bQ |= bit; break;
						case 'k': bK |= bit; break;

						default:
							throw new ArgumentException(
								$"Illegal piece character ‘{c}’ in FEN.", nameof(placementPart));
					}

					file++; // next file
				}

				if (file != 8)
				{
					throw new ArgumentException(
						$"Rank “{rank}” in FEN does not contain 8 files.", nameof(placementPart));
				}

				rankOffset -= 8; // Move one rank down
			}

			// Build immutable bitboard objects
			var white = new ColorBitboards(wP, wN, wB, wR, wQ, wK);
			var black = new ColorBitboards(bP, bN, bB, bR, bQ, bK);

			BoardBitboards bitboards = new(white, black);

			// The Board record / class in the engine has a ctor that takes the 12 bitboards.
			return new Board(bitboards);
		}

		private static CastlingRights ParseCastlingRights(string castlingPart)
		{
			var rights = CastlingRights.None;
			if (castlingPart.Contains('K'))
			{
				rights |= CastlingRights.WhiteKingside;
			}

			if (castlingPart.Contains('Q'))
			{
				rights |= CastlingRights.WhiteQueenside;
			}

			if (castlingPart.Contains('k'))
			{
				rights |= CastlingRights.BlackKingside;
			}

			if (castlingPart.Contains('q'))
			{
				rights |= CastlingRights.BlackQueenside;
			}

			return rights;
		}

		private static Piece FenCharToPiece(char fenChar)
		{
			PieceColor color = char.IsUpper(fenChar) ? PieceColor.White : PieceColor.Black;
			PieceType type = char.ToLower(fenChar) switch
			{
				'p' => PieceType.Pawn,
				'n' => PieceType.Knight,
				'b' => PieceType.Bishop,
				'r' => PieceType.Rook,
				'q' => PieceType.Queen,
				'k' => PieceType.King,
				_   => throw new ArgumentException($"Invalid FEN piece character: {fenChar}")
			};

			return new Piece(type, color);
		}

		private static PieceColor ParseActiveColor(string colorPart) => colorPart switch
		{
			"w" => PieceColor.White,
			"b" => PieceColor.Black,
			_   => throw new ArgumentException($"Invalid active color specifier: {colorPart}")
		};

		private static Position ParseEnPassantTarget(string enPassantPart)
		{
			if (enPassantPart == "-")
			{
				return default;
			}

			return new Position(enPassantPart);
		}

		private static uint ParseFullMoveNumber(string fullMovePart)
		{
			if (!int.TryParse(fullMovePart, out int value) || value <= 0)
			{
				throw new ArgumentException($"Invalid full-move number value: {fullMovePart}");
			}

			return (uint)value;
		}

		private static uint ParseHalfMoveClock(string halfMovePart)
		{
			if (!int.TryParse(halfMovePart, out int value) || value < 0)
			{
				throw new ArgumentException($"Invalid half-move clock value: {halfMovePart}");
			}

			return (uint)value;
		}
	}
}
