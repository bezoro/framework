using System;

namespace Bezoro.Core.Collections.Array
{
	/// <summary>
	///     A structure to hold detailed information about an array and its data.
	/// </summary>
	/// <typeparam name="T">The type of the elements in the array.</typeparam>
	public readonly struct ArrayElementInfo<T>
	{
		/// <summary>
		///     Constructor for creating an Array_Element_Info instance.
		/// </summary>
		/// <param name="relevantIndex">The index of the element in the array.</param>
		/// <param name="element">The specific array element.</param>
		/// <param name="length">The length of the array.</param>
		public ArrayElementInfo(int relevantIndex, T element, int length)
		{
			RelevantIndex = relevantIndex;
			Element       = element;
			Length        = length;
		}

		/// <summary>
		///     The total length of the array.
		/// </summary>
		public int Length { get; }
		/// <summary>
		///     The index of the specific element in the array.
		/// </summary>
		public int RelevantIndex { get; }

		/// <summary>
		///     The element itself.
		/// </summary>
		public T Element { get; }

		/// <summary>
		///     The type of the array.
		/// </summary>
		public Type ArrayType => typeof(T);

		/// <summary>
		///     The element's type in the array.
		/// </summary>
		public Type ElementType => Element?.GetType() ?? typeof(T);

		/// <summary>
		///     Returns a string representation of the array data.
		/// </summary>
		/// <returns>A string detailing the array data.</returns>
		public override string ToString() =>
			$"Array Data: [Index: {RelevantIndex}, Element: {Element}, Array Length: {Length}, Array Type: {ArrayType}]";
	}
}
