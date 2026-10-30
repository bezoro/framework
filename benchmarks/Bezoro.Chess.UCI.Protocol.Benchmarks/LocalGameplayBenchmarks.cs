using BenchmarkDotNet.Attributes;
using Bezoro.Chess.UCI.Protocol.API;
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using Bezoro.Chess.UCI.Protocol.API.Types;

namespace Bezoro.Chess.UCI.Protocol.Benchmarks;

/// <summary>Measures local play separately from engine analysis and process startup.</summary>
[MemoryDiagnoser]
public class LocalGameplayBenchmarks
{
    private static readonly string[] Opening =
        ["e2e4", "e7e5", "g1f3", "b8c6", "f1b5", "a7a6", "b5a4", "g8f6"];

    [Benchmark]
    public Fen ApplyOpeningMove() => Fen.Default.ApplyMove("e2e4");

    [Benchmark]
    public int GenerateOpeningMoves() => Fen.Default.GetLegalMoves().Length;

    [Benchmark(OperationsPerInvoke = 8)]
    public PlayableMatchState PlayEightLocalPlies()
    {
        var match = new UciPlayableMatchSession();
        var state = match.Refresh();
        foreach (string move in Opening)
        {
            match.ApplyMove(move);
            state = match.Refresh();
        }
        return state;
    }
}
