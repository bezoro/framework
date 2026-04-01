using BenchmarkDotNet.Attributes;
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using Bezoro.Chess.UCI.Protocol.API.Types;

namespace Bezoro.Chess.UCI.Protocol.Benchmarks;

[MemoryDiagnoser]
public class LocalRulesBenchmarks
{
	private static readonly Fen OpeningFen = Fen.Default;
	private static readonly Fen MiddlegameFen =
		Fen.Parse("r2q1rk1/pp2bppp/2n1pn2/2bp4/2P5/2NP1NP1/PP2PPBP/R1BQ1RK1 w - - 4 10")!.Value;

	[Benchmark(Description = "Legal moves from opening position")]
	public int GetLegalMoves_Opening()
	{
		var legalMoves = OpeningFen.GetLegalMoves();
		return legalMoves.Length;
	}

	[Benchmark(Description = "Legal moves from middlegame position")]
	public int GetLegalMoves_Middlegame()
	{
		var legalMoves = MiddlegameFen.GetLegalMoves();
		return legalMoves.Length;
	}

	[Benchmark(Description = "Fully classify opening legal moves")]
	public int ClassifyMovesFully_Opening()
	{
		var classifications = OpeningFen.ClassifyMovesFully(OpeningFen.GetLegalMoves());
		return classifications.Count;
	}

	[Benchmark(Description = "Fully classify middlegame legal moves")]
	public int ClassifyMovesFully_Middlegame()
	{
		var legalMoves = MiddlegameFen.GetLegalMoves();
		var classifications = MiddlegameFen.ClassifyMovesFully(legalMoves);
		return classifications.Count;
	}
}
