using System;
using System.Text;

namespace Bezoro.Core.Common.Extensions
{
	public static class StringExtensions
	{
		/// <summary>
		///     Repeats a string a specified number of times.
		/// </summary>
		/// <param name="str">The string to repeat</param>
		/// <param name="count">The number of times to repeat the string</param>
		/// <returns>A new string containing the original string repeated the specified number of times</returns>
		public static string Repeat(this string str, int count)
		{
			if (str == null)
			{
				throw new ArgumentNullException(nameof(str));
			}

			if (count < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative");
			}

			if (count == 0 || string.IsNullOrEmpty(str))
			{
				return string.Empty;
			}

			if (count == 1)
			{
				return str;
			}

			var sb = new StringBuilder(str.Length * count);
			for (var i = 0 ; i < count ; i++)
			{
				sb.Append(str);
			}

			return sb.ToString();
		}
	}
}
