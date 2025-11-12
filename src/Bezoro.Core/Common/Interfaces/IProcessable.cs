namespace Bezoro.Core.Common.Interfaces;

/// <summary>
/// Represents a type that can perform a processing operation.
/// </summary>
public interface IProcessable
{
    /// <summary>
    /// Executes the processing logic for the implementing type.
    /// </summary>
    void Process();
}
