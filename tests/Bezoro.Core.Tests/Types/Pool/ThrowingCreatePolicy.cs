using System;
using Bezoro.Core.Abstractions;

namespace Bezoro.Core.Tests.Types.Pool;

internal sealed class ThrowingCreatePolicy(Exception exception) : IPoolPolicy<object>
{
	public int CreateCount { get; private set; }

	public object Create()
	{
		CreateCount++;
		throw exception;
	}

	public void OnDiscard(object item) { }
	public bool Reset(object item) => true;
	public bool Validate(object item) => true;
}
