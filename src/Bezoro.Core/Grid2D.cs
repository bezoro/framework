using System;

namespace Bezoro.Core
{
	/// <summary>
	///     Represents a two-dimensional grid of elements of type T.
	/// </summary>
	/// <typeparam name="T">The type of elements stored in the grid.</typeparam>
	[Serializable]
	public class Grid2D<T> : Grid2D
	{
		private readonly T[,] _data;

		/// <summary>
		///     Initializes a new instance of the <see cref="Grid2D{T}" /> class.
		/// </summary>
		/// <param name="width">The width (first dimension length) of the grid.</param>
		/// <param name="height">The height (second dimension length) of the grid.</param>
		/// <exception cref="ArgumentException">Thrown if width or height are not greater than zero.</exception>
		public Grid2D(int width, int height) : base(width, height)
		{
			_data = new T[width, height];
		}

		/// <summary>
		///     Initializes a new instance of the <see cref="Grid2D{T}" /> class with a default value.
		/// </summary>
		/// <param name="width">The width (first dimension length) of the grid.</param>
		/// <param name="height">The height (second dimension length) of the grid.</param>
		/// <param name="defaultValue">The default value to initialize all grid elements with.</param>
		/// <exception cref="ArgumentException">Thrown if width or height are not greater than zero.</exception>
		public Grid2D(int width, int height, T defaultValue) : this(width, height)
		{
			for (var x = 0 ; x < width ; x++)
			{
				for (var y = 0 ; y < height ; y++)
					_data[x, y] = defaultValue;
			}
		}

		/// <summary>
		///     Gets or sets the element at the specified coordinates in the grid.
		/// </summary>
		/// <param name="x">The x-coordinate (first dimension index).</param>
		/// <param name="y">The y-coordinate (second dimension index).</param>
		/// <returns>The element at the specified coordinates.</returns>
		/// <exception cref="IndexOutOfRangeException">Thrown when coordinates are outside the grid bounds.</exception>
		public T this[int x, int y]
		{
			get => _data[x, y];
			set => _data[x, y] = value;
		}
	}

	[Serializable]
	public class Grid2D
	{
		/// <summary>
		///     Initializes a new instance of the <see cref="Grid2D" /> class.
		/// </summary>
		/// <param name="width">The width (first dimension length) of the grid.</param>
		/// <param name="height">The height (second dimension length) of the grid.</param>
		/// <exception cref="ArgumentException">Thrown if width or height are not greater than zero.</exception>
		public Grid2D(int width, int height)
		{
			if (width <= 0 || height <= 0)
				throw new ArgumentException("Grid dimensions must be greater than zero.");

			Width  = width;
			Height = height;
		}

		/// <summary>
		///     Gets the height (length of the second dimension) of the grid.
		/// </summary>
		public int Height { get; }

		/// <summary>
		///     Gets the width (length of the first dimension) of the grid.
		/// </summary>
		public int Width { get; }
	}
}
