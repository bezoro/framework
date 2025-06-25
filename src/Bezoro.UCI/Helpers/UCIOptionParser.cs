using System;
using System.Collections.Generic;
using Bezoro.UCI.API.Types;

namespace Bezoro.UCI.Helpers
{
	/// <summary>
	///     Helper class for parsing UCI engine option strings.
	/// </summary>
	internal static class UCIOptionParser
	{
		/// <summary>
		///     Parses a UCI option line from the engine output.
		/// </summary>
		public static UCIOption ParseOptionLine(string line)
		{
			string[] parts        = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
			string   name         = null, type = null, defaultValue = null, min = null, max = null;
			var      vars         = new List<string>();
			var      isVarSection = false;

			for (var i = 0 ; i < parts.Length ; i++)
			{
				switch (parts[i])
				{
					case "name":    name         = parts[++i]; break;
					case "type":    type         = parts[++i]; break;
					case "default": defaultValue = parts[++i]; break;
					case "min":     min          = parts[++i]; break;
					case "max":     max          = parts[++i]; break;
					case "var":
						isVarSection = true;
						break;
					default:
						if (isVarSection)
						{
							vars.Add(parts[i]);
						}

						break;
				}
			}

			return new UCIOption(name, type, defaultValue, min, max, vars.ToArray());
		}
	}
}
