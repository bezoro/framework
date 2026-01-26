using System;
using Bezoro.Core.Abstractions;

namespace Bezoro.Core.Tests.Types.Pool;

internal sealed class DisposableTestObject : IDisposable
{
	public bool IsDisposed { get; private set; }
	public void Dispose()  => IsDisposed = true;
}

internal sealed class TestPooledObject : IPooledObject
{
	public bool ReturnValue   { get; set; } = true;
	public int  OnRentCount   { get; private set; }
	public int  OnReturnCount { get; private set; }

	public bool OnReturn()
	{
		OnReturnCount++;
		return ReturnValue;
	}

	public void OnRent() => OnRentCount++;
}
