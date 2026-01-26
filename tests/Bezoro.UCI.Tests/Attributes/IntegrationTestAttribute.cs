namespace Bezoro.UCI.Tests.Attributes;

/// <summary>
///     Marks a test as an integration test that requires external resources.
///     Use this instead of [Fact] for integration tests.
///     Automatically adds the "Category" trait with value "Integration".
/// </summary>
public class IntegrationTestAttribute : FactAttribute { }
