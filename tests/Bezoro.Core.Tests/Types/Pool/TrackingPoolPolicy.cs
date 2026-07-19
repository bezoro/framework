using Bezoro.Core.Abstractions;

namespace Bezoro.Core.Tests.Types.Pool;

internal sealed class TrackingPoolPolicy : IPoolPolicy<object>
{
	public int CreateCount { get; private set; }
	public int DiscardCount { get; private set; }
	public int ResetCount { get; private set; }
	public int ValidateCount { get; private set; }
	public bool ResetResult { get; set; } = true;
	public bool ValidateResult { get; set; } = true;

	public object Create()
	{
		CreateCount++;
		return new();
	}

	public void OnDiscard(object item) => DiscardCount++;

	public bool Reset(object item)
	{
		ResetCount++;
		return ResetResult;
	}

	public bool Validate(object item)
	{
		ValidateCount++;
		return ValidateResult;
	}
}
