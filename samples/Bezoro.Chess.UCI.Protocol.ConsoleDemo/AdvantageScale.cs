namespace Bezoro.Chess.UCI.Protocol.ConsoleDemo;

internal static class AdvantageScale
{
	private const double MaxNormalizedCp = 0.95;
	private const double CpScale = 1_000.0;

	public static double NormalizeCp(int cp)
	{
		if (cp == 0)
			return 0;

		double magnitude = Math.Tanh(Math.Abs(cp) / CpScale) * MaxNormalizedCp;
		return cp > 0 ? magnitude : -magnitude;
	}
}
