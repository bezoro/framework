using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Bezoro.Chess.API.Extensions;
using Bezoro.Chess.API.ViewModels;
using Bezoro.Chess.Domain.Extensions;
using Bezoro.Chess.Domain.Functions.Moves;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;
using Bezoro.Core;
using MoveType = Bezoro.Chess.API.Shared.Enums.MoveType;
using PromotionType = Bezoro.Chess.Domain.Shared.Enums.PromotionType;

namespace Bezoro.Chess.API.Engine
{
	/// <summary>
	///     Entrypoint to the system
	/// </summary>
	public record ChessEngine
	{
		private GameState _state;

		public GameStateViewModel GetGameState() =>
			_state.ToViewModel();

		public Result<ImmutableArray<MoveViewModel>> GetCurrentLegalMoves()
		{
			IEnumerable<Move> moves = MoveGenerator.GenerateMoves(_state);

			var viewModels = new List<MoveViewModel>();
			foreach (Move move in moves)
			{
				viewModels.Add(new MoveViewModel(move));
			}

			Result<ImmutableArray<MoveViewModel>> result = Result.Succeeded(viewModels.ToImmutableArray());
			return result;
		}

		public Result<MoveViewModel> TryApplyMove(MoveViewModel moveViewModel)
		{
			Move          move;
			Position      from           = moveViewModel.From.ToDomain();
			Position      to             = moveViewModel.To.ToDomain();
			MoveType      type           = moveViewModel.Type;
			Piece         piece          = moveViewModel.Piece.ToDomain();
			Piece         capturedPiece  = moveViewModel.CapturedPiece.ToDomain();
			PromotionType promotionPiece = moveViewModel.PromotionPieceType.ToDomain();

			switch (type)
			{
				case MoveType.None:
					throw new InvalidOperationException("Invalid move");
				case MoveType.Normal:
					move = Move.CreateNormal(from, to, piece);
					break;
				case MoveType.Capture:
					move = Move.CreateCapture(from, to, piece, capturedPiece);
					break;
				case MoveType.CastleKingside:
					move = Move.CreateCastleKingside(from, to, piece);
					break;
				case MoveType.CastleQueenside:
					move = Move.CreateCastleQueenside(from, to, piece);
					break;
				case MoveType.EnPassant:
					move = Move.CreateEnPassant(from, to, piece, capturedPiece);
					break;
				case MoveType.QuietPromotion:
					move = Move.CreateQuietPromotion(from, to, piece, promotionPiece);
					break;
				case MoveType.CapturePromotion:
					move = Move.CreateCapturePromotion(from, to, piece, capturedPiece, promotionPiece);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			_state = MoveExecution.ExecuteMove(_state, move);
			return Result<MoveViewModel>.Succeeded(moveViewModel);
		}
	}
}
