using System;

namespace Bezoro.Core.Common.Helpers
{
	public static class ArrayConverter
	{
		public static T[] From2Dto1D<T>(T[,] from)
		{
			if (from == null)
			{
				throw new ArgumentNullException(nameof(from));
			}

			int rows   = from.GetLength(0);
			int cols   = from.GetLength(1);
			var result = new T[rows * cols];

			for (var i = 0 ; i < rows ; i++)
			{
				for (var j = 0 ; j < cols ; j++)
				{
					result[i * cols + j] = from[i, j];
				}
			}

			return result;
		}
	}
}
