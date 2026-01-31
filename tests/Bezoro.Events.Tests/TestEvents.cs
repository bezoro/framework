namespace Bezoro.Events.Tests;

public readonly record struct TestEventA(int Value);

public readonly record struct TestEventB(string Message);

public readonly record struct TestEventC(double Amount);
