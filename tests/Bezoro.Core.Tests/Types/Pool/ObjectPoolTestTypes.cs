using System;
using Bezoro.Core.Abstractions;

namespace Bezoro.Core.Tests.Types.Pool;

internal sealed class DisposableObject : IDisposable
{
	public bool IsDisposed { get; private set; }
	public void Dispose()  => IsDisposed = true;
}

internal sealed class TestObject : IPooledObject
{
	public bool IsValid     { get; set; } = true;
	public int  RentCount   { get; private set; }
	public int  ReturnCount { get; private set; }

	public bool OnReturn()
	{
		ReturnCount++;
		return IsValid;
	}

	public void OnRent() => RentCount++;
}
