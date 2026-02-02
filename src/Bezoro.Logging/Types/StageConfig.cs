namespace Bezoro.Logging.Types;

/// <summary>
///     Configuration for stage metadata.
/// </summary>
public readonly struct StageConfig
{
	/// <summary>
	///     Creates an enabled stage configuration with the specified provider.
	/// </summary>
	/// <param name="provider">Function that returns the current stage label (e.g., "Entering Play Mode").</param>
	/// <param name="dividerProvider">Function that returns the divider line for stage changes.</param>
	public static StageConfig Create(
		Func<string?>                    provider,
		Func<string?, string?, string?>? dividerProvider = null) =>
		new()
		{
			Enabled         = true,
			Provider        = provider,
			DividerProvider = dividerProvider ?? DefaultDividerProvider
		};

	/// <summary>
	///     Disabled stage configuration.
	/// </summary>
	public static StageConfig Disabled =>
		new() { Enabled = false, Provider = null, DividerProvider = null };

	/// <summary>
	///     Whether stage metadata is enabled.
	/// </summary>
	public bool Enabled { get; init; }

	/// <summary>
	///     Divider line provider to emit when the stage changes.
	/// </summary>
	public Func<string?, string?, string?>? DividerProvider { get; init; }

	/// <summary>
	///     Provider function that returns the current stage label.
	/// </summary>
	public Func<string?>? Provider { get; init; }

	/// <summary>
	///     Default divider line emitted on stage changes.
	/// </summary>
	public static string? DefaultDividerProvider(string? fromStage, string? toStage)
	{
		if (string.IsNullOrWhiteSpace(fromStage) || string.IsNullOrWhiteSpace(toStage))
			return null;

		return $"[{fromStage}] ==============> [{toStage}]";
	}
}
