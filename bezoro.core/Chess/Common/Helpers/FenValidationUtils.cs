namespace Bezoro.Core.Chess.Utils
{
	internal static class FenValidators
	{
		public static bool IsValidPiecePlacement(string field)
		{
			string[] ranks = field.Split('/');
			if (ranks.Length != 8) return false;

			foreach (var rank in ranks)
			{
				var files = 0;
				foreach (var c in rank)
				{
					if (char.IsDigit(c)) files                     += c - '0';
					else if ("prnbqkPRNBQK".IndexOf(c) >= 0) files += 1;
					else return false;
				}

				if (files != 8) return false;
			}

			return true;
		}

		public static bool TryParseEnPassant(string token, out string square)
		{
			square = token;
			if (token == "-") return true;

			return token.Length == 2 && "abcdefgh".Contains(token[0]) && "36".Contains(token[1]);
		}
	}
}
