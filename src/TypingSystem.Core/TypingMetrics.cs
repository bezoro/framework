using System;
using System.Diagnostics;

namespace TypingSystem.Core;

public sealed class TypingMetrics
{
	private readonly Stopwatch _stopwatch;

	public TypingMetrics()
	{
		_stopwatch = Stopwatch.StartNew();
	}

	public uint TotalInputs { get; private set; }

	public uint CorrectInputs { get; private set; }

	public uint MistakeInputs { get; private set; }

	public uint FaultedInputs { get; private set; }

	public TimeSpan Elapsed => _stopwatch.Elapsed;

	public uint TotalEvaluated => TotalInputs - FaultedInputs;

	public double Accuracy => TotalEvaluated == 0 ? 1d : (double)CorrectInputs / TotalEvaluated;

	public double CharactersPerMinute => Elapsed.TotalMinutes <= 0 ? 0d : CorrectInputs / Elapsed.TotalMinutes;

	public void Record(TypingResult result)
	{
		TotalInputs++;

		if (result.IsFaulted)
		{
			FaultedInputs++;
			return;
		}

		if (result.IsCorrect)
		{
			CorrectInputs++;
		}
		else
		{
			MistakeInputs++;
		}
	}

	public void Reset()
	{
		TotalInputs = 0;
		CorrectInputs = 0;
		MistakeInputs = 0;
		FaultedInputs = 0;
		_stopwatch.Restart();
	}
}
