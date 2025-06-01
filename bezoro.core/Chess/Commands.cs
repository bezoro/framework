namespace Bezoro.Core.Chess
{
	public struct MoveCommand : IChessCommand
	{
		public MoveCommand(ChessPosition from, ChessPosition to)
		{
			From = from;
			To   = to;
		}

		public ChessPosition From { get; }

		public ChessPosition To { get; }
	}
}
