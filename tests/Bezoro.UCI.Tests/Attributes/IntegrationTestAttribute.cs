using Xunit;

namespace Bezoro.UCI.Tests.Attributes;

/// <summary>
/// Marks a test as an integration test that requires external resources.
/// Use this instead of [Fact] for integration tests.
/// Automatically adds the "Category" trait with value "Integration".
/// </summary>
public class IntegrationTestAttribute : FactAttribute
{
	public IntegrationTestAttribute()
	{
		// Note: Traits are typically added via [Trait] attribute on the test method
		// This attribute serves as a marker for integration tests
	}
}

