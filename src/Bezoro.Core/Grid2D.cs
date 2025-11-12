using System;

namespace Bezoro.Core
{
    /// <summary>
    /// Represents a two-dimensional grid of elements of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements stored in the grid.</typeparam>
    [Serializable]
    public class Grid2D<T> : Grid2D
    {
        /// <summary>
        /// Backing storage for the grid elements.
        /// </summary>
        private readonly T[,] _data;

        /// <summary>
        /// Initializes a new instance of the <see cref="Grid2D{T}"/> class with the specified dimensions.
        /// </summary>
        /// <param name="width">The width (number of columns) of the grid. Must be greater than zero.</param>
        /// <param name="height">The height (number of rows) of the grid. Must be greater than zero.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="width"/> or <paramref name="height"/> are not greater than zero.</exception>
        public Grid2D(int width, int height) : base(width, height)
        {
            _data = new T[width, height];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Grid2D{T}"/> class with the specified dimensions and default value for all elements.
        /// </summary>
        /// <param name="width">The width (number of columns) of the grid. Must be greater than zero.</param>
        /// <param name="height">The height (number of rows) of the grid. Must be greater than zero.</param>
        /// <param name="defaultValue">The default value to assign to all elements in the grid.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="width"/> or <paramref name="height"/> are not greater than zero.</exception>
        public Grid2D(int width, int height, T defaultValue) : this(width, height)
        {
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < height; y++)
                {
                    _data[x, y] = defaultValue;
                }
            }
        }

        /// <summary>
        /// Gets or sets the element at the specified <paramref name="x"/> and <paramref name="y"/> coordinates.
        /// </summary>
        /// <param name="x">The column index (zero-based).</param>
        /// <param name="y">The row index (zero-based).</param>
        /// <returns>The element located at the given coordinates.</returns>
        /// <exception cref="IndexOutOfRangeException">Thrown if the specified coordinates are outside the valid bounds of the grid.</exception>
        public T this[int x, int y]
        {
            get => _data[x, y];
            set => _data[x, y] = value;
        }
    }

    /// <summary>
    /// Represents the non-generic base class for two-dimensional grids.
    /// Provides grid dimensions and basic validation logic.
    /// </summary>
    [Serializable]
    public class Grid2D
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Grid2D"/> class with the specified dimensions.
        /// </summary>
        /// <param name="width">The width (number of columns) of the grid. Must be greater than zero.</param>
        /// <param name="height">The height (number of rows) of the grid. Must be greater than zero.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="width"/> or <paramref name="height"/> are not greater than zero.</exception>
        public Grid2D(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentException("Grid dimensions must be greater than zero.");
            Width = width;
            Height = height;
        }

        /// <summary>
        /// Gets the height (number of rows) of the grid.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the width (number of columns) of the grid.
        /// </summary>
        public int Width { get; }
    }
}
