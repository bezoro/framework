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

		public Result<GameStateViewModel> StartNewGame()
		{
			_state = GameState.CreateInitial();
			return Result.Succeeded(_state.ToViewModel());
		}

		/// <summary>
		///     Generates and returns all legal moves available in the current game state.
		/// </summary>
		/// <returns>A Result containing an immutable array of legal moves represented as MoveViewModel objects if successful.</returns>
		/// <remarks>
		///     The method uses MoveGenerator to create all possible legal moves from the current game state
		///     and converts them to view models for external use.
		/// </remarks>
		public Result<ImmutableArray<MoveViewModel>> GetCurrentLegalMoves()
		{
			IEnumerable<Move> moves = MoveGenerator.GenerateMoves(_state);

			var viewModels = new List<MoveViewModel>();
			foreach (Move move in moves)
			{
				viewModels.Add(new MoveViewModel(move));
			}

			return Result.Succeeded(viewModels.ToImmutableArray());
		}

		/// <summary>
		///     Attempts to apply a chess move to the current game state using the provided move view model.
		/// </summary>
		/// <param name="moveViewModel">The view model containing the move details to be applied.</param>
		/// <returns>A Result containing the applied move view model if successful.</returns>
		/// <exception cref="InvalidOperationException">Thrown when the move type is invalid (None).</exception>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when the move type is not recognized.</exception>
		public Result<MoveViewModel> TryApplyMove(MoveViewModel moveViewModel)
		{
			Move          move;
			Position      from           = moveViewModel.From.ToDomain();
			Position      to             = moveViewModel.To.ToDomain();
			MoveType      type           = moveViewModel.Type;
			Piece         piece          = moveViewModel.Piece.ToDomain();
			Piece?        capturedPiece  = moveViewModel.CapturedPiece.Value.ToDomain();
			PromotionType promotionPiece = moveViewModel.PromotionPieceType.ToDomain();

			switch (type)
			{
				case MoveType.None:
					throw new InvalidOperationException("Invalid move");
				case MoveType.Normal:
					move = Move.Normal(from, to, piece);
					break;
				case MoveType.Capture:
					move = Move.Capture(from, to, piece, capturedPiece.Value);
					break;
				case MoveType.CastleKingside:
					move = Move.CastleKingside(from, to, piece);
					break;
				case MoveType.CastleQueenside:
					move = Move.CastleQueenside(from, to, piece);
					break;
				case MoveType.EnPassant:
					move = Move.EnPassant(from, to, piece, capturedPiece.Value);
					break;
				case MoveType.QuietPromotion:
					move = Move.Promotion(from, to, piece, promotionPiece);
					break;
				case MoveType.CapturePromotion:
					move = Move.PromotionCapture(from, to, piece, capturedPiece.Value, promotionPiece);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			_state = MoveExecution.ExecuteMove(_state, move);
			return Result.Succeeded(moveViewModel);
		}
	}
}
