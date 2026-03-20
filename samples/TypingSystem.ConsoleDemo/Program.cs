using Bezoro.TypingSystem.Abstractions;
using Bezoro.TypingSystem.Types;
using Bezoro.TypingSystem.Utilities;

Console.Title = "TypingSystem Console Demo";
Console.WriteLine("TypingSystem Console Demo\n----------------------------");
Console.WriteLine("Press ESC at any time to exit. Type the prompted word character by character.\n");

IWordProvider wordProvider = new ArrayWordProvider(
	new[]
	{
		"Cursor",
		"Framework",
		"Validator",
		"Metrics",
		"Refactor"
	}
);

var metrics = new TypingMetrics();
var options = new TypingValidatorOptions
{
	IgnoreCase = true,
	Metrics    = metrics,
	OnMatch    = _ => Console.Write('+'),
	OnMismatch = result =>
	{
		Console.Write('\b');
		Console.Write('✗');
		Console.WriteLine($"\nMismatch: expected '{result.Expected}', received '{result.Input}'.");
	},
	OnCompleted = result => { Console.WriteLine($"\nWord completed! Next position: {result.NextPosition}."); },
	OnFault     = result => Console.WriteLine($"\nFaulted: {result.Status}.")
};

while (wordProvider.HasMoreWords)
{
	var wordMemory  = wordProvider.GetNextWord();
	var targetSpan  = wordMemory.Span;
	var displayWord = wordMemory.ToString();

	Console.WriteLine($"\nType this word: {displayWord}");
	Console.WriteLine(new string('─', displayWord.Length));

	var state = TypingState.Initial;

	var completed = false;

	while (!completed)
	{
		var key = Console.ReadKey(true);

		if (key.Key == ConsoleKey.Escape)
		{
			Console.WriteLine("\nSession aborted by user.");
			Summarize(metrics);
			return;
		}

		char inputChar = key.KeyChar;
		if (char.IsControl(inputChar)) continue;

		var result = TypingValidator.ValidateInput(targetSpan, state.Position, inputChar, options);

		switch (result.Status)
		{
			case TypingValidationStatus.Match:
				state = state.WithCorrect();
				Console.Write(inputChar);
				break;
			case TypingValidationStatus.Completed:
				state = state.WithCorrect();
				Console.Write(inputChar);
				completed = true;
				break;
			case TypingValidationStatus.Mismatch:
				state = state.WithMistake();
				break;
			case TypingValidationStatus.PositionOutOfRange:
				state = state.WithMistake();
				Console.WriteLine("\nInput exceeded target length. Moving to next word.");
				completed = true;
				break;
			case TypingValidationStatus.EmptyTarget:
				completed = true;
				break;
		}
	}

	Console.WriteLine($"\nMistakes this round: {state.MistakeCount}\n");
}

Summarize(metrics);

static void Summarize(TypingMetrics metrics)
{
	Console.WriteLine("==============================");
	Console.WriteLine("Session Summary");
	Console.WriteLine("==============================");
	Console.WriteLine($"Total inputs: {metrics.TotalInputs}");
	Console.WriteLine($"Correct inputs: {metrics.CorrectInputs}");
	Console.WriteLine($"Mistakes: {metrics.MistakeInputs}");
	Console.WriteLine($"Faulted inputs: {metrics.FaultedInputs}");
	Console.WriteLine($"Accuracy: {metrics.Accuracy:P1}");
	Console.WriteLine($"Elapsed: {metrics.Elapsed:mm\\:ss}");
	Console.WriteLine($"Characters per minute: {metrics.CharactersPerMinute:F1}");
}
