namespace Bezoro.ECS.Types;

/// <summary>
///     Describes scheduler batches for one stage.
/// </summary>
/// <param name="stage">Stage represented by this diagnostics node.</param>
/// <param name="batches">Batches scheduled in this stage.</param>
public sealed class ScheduleStageDiagnostics(Stage stage, ScheduleBatchDiagnostics[] batches)
{
	/// <summary>
	///     Gets the batches scheduled in this stage.
	/// </summary>
	public ScheduleBatchDiagnostics[] Batches { get; } = batches ?? throw new ArgumentNullException(nameof(batches));

	/// <summary>
	///     Gets the stage represented by this diagnostics node.
	/// </summary>
	public Stage Stage { get; } = stage;
}
