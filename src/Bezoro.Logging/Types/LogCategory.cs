namespace Bezoro.Logging.Types;

/// <summary>
///     Specifies the category of a log message for classification and filtering.
/// </summary>
public enum LogCategory
{
	// Core

	/// <summary>
	///     Default/general log category.
	/// </summary>
	Default,

	/// <summary>
	///     System-level log messages.
	/// </summary>
	System,

	/// <summary>
	///     Other uncategorized log messages.
	/// </summary>
	Other,

	// Development & Debugging

	/// <summary>
	///     Messages related to debugging.
	/// </summary>
	Debug,

	/// <summary>
	///     Messages related to tests.
	/// </summary>
	Test,

	/// <summary>
	///     Profiling or performance-analysis messages.
	/// </summary>
	Profiling,

	/// <summary>
	///     Editor-specific messages.
	/// </summary>
	Editor,

	/// <summary>
	///     Analytics and telemetry messages.
	/// </summary>
	Analytics,

	/// <summary>
	///     Performance marker messages.
	/// </summary>
	Performance,

	// System Core

	/// <summary>
	///     Memory-related messages.
	/// </summary>
	Memory,

	/// <summary>
	///     Security, authorization, or permission-related messages.
	/// </summary>
	Security,

	/// <summary>
	///     Database access or query messages.
	/// </summary>
	Database,

	/// <summary>
	///     File input/output messages.
	/// </summary>
	FileIO,

	/// <summary>
	///     Configuration or settings-related log messages.
	/// </summary>
	Configuration,

	/// <summary>
	///     Loading processes and progress updates.
	/// </summary>
	Loading,

	/// <summary>
	///     Save/load system-related log messages.
	/// </summary>
	SaveSystem,

	/// <summary>
	///     Asset and resource management messages.
	/// </summary>
	Resources,

	/// <summary>
	///     Asset bundle-related log category.
	/// </summary>
	AssetBundle,

	/// <summary>
	///     Scene management and transitions.
	/// </summary>
	SceneManagement,

	// Rendering & Graphics

	/// <summary>
	///     Rendering and visual graphics messages.
	/// </summary>
	Rendering,

	/// <summary>
	///     Particle system messages.
	/// </summary>
	Particles,

	/// <summary>
	///     Shader and graphics pipeline messages.
	/// </summary>
	Shaders,

	/// <summary>
	///     Post-processing and image effect messages.
	/// </summary>
	PostProcessing,

	/// <summary>
	///     Lighting engine or lighting effect messages.
	/// </summary>
	Lighting,

	// Game Systems

	/// <summary>
	///     Gameplay-specific or mechanics messages.
	/// </summary>
	Gameplay,

	/// <summary>
	///     Combat system messages.
	/// </summary>
	Combat,

	/// <summary>
	///     Inventory system messages.
	/// </summary>
	Inventory,

	/// <summary>
	///     Quest system messages.
	/// </summary>
	Quest,

	/// <summary>
	///     Dialogue system messages.
	/// </summary>
	Dialog,

	/// <summary>
	///     Procedural or dynamic level generation messages.
	/// </summary>
	LevelGeneration,

	/// <summary>
	///     Player or system progression-related messages.
	/// </summary>
	Progression,

	/// <summary>
	///     Achievement system messages.
	/// </summary>
	Achievement,

	/// <summary>
	///     Economy or in-game currency messages.
	/// </summary>
	Economy,

	// Input & UI

	/// <summary>
	///     Player or system input messages.
	/// </summary>
	Input,

	/// <summary>
	///     User interface/in-game UI messages.
	/// </summary>
	UI,

	// Animation & Physics

	/// <summary>
	///     Animation subsystem messages.
	/// </summary>
	Animation,

	/// <summary>
	///     Physics engine or simulation messages.
	/// </summary>
	Physics,

	/// <summary>
	///     2D physics system messages.
	/// </summary>
	Physics2D,

	// AI & Behavior

	/// <summary>
	///     Artificial intelligence or behavior-tree messages.
	/// </summary>
	AI,

	// Audio

	/// <summary>
	///     Audio subsystem messages.
	/// </summary>
	Audio,

	// Networking & Services

	/// <summary>
	///     Networking, communication, or multiplayer messages.
	/// </summary>
	Network,

	/// <summary>
	///     Cloud service/messaging messages.
	/// </summary>
	Cloud,

	/// <summary>
	///     Authentication and login messages.
	/// </summary>
	Authentication,

	/// <summary>
	///     Online services, friends, or social integration messages.
	/// </summary>
	Social,

	/// <summary>
	///     Purchasing, microtransactions, or commerce system messages.
	/// </summary>
	Purchasing,

	// Localization

	/// <summary>
	///     Localization, language, or translation-related messages.
	/// </summary>
	Localization,

	// Other

	/// <summary>
	///     Camera subsystem or visual frustum messages.
	/// </summary>
	Camera,

	/// <summary>
	///     Game/system initialization messages.
	/// </summary>
	Initialization,

	/// <summary>
	///     Utility and helper methods or modules.
	/// </summary>
	Utilities,

	/// <summary>
	///     Universal Chess Interface or other application-specific protocols.
	/// </summary>
	UCI
}
