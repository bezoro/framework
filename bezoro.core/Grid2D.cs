using System;

namespace Bezoro.Core
{
	[Serializable]
	public class Grid2D<T> : Grid2D
	{
		public T[,] Data { get; }

		public int Char_To_Int(char letter) => letter - 'A' + 1;

		public char Int_To_Char(int number) => (char)(number - 1 + 'A');

		public Grid2D(T[,] data) : base(data.GetLength(0), data.GetLength(1))
		{
			Data = data;
		}
	}

	public class Grid2D
	{
		private const int _MAX_VALUE = 26;
		private const int _MIN_VALUE = 1;

		public Grid2D(int width, int height)
		{
			Width  = width;
			Height = height;

			Logger.LogSuccess($"{nameof(Grid2D)} created with size: {Width}x{Height}.");
		}

		public int Height { get; }
		public int Width  { get; }
	}
}
