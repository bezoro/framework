using System.Collections;
using System.Diagnostics;
using Bezoro.Core.Logging;

namespace Bezoro.Core;

/// <summary>
///     Provides utility methods to get emoji representations for log categories.
/// </summary>
public static class LogCategoryEmoji
{
	private static readonly Dictionary<LogCategory, string> CategoryToEmoji = new()
	{
		// Core
		{ LogCategory.Default, "🔄" },
		{ LogCategory.System, "⚙️" },
		{ LogCategory.Other, "❓" },

		// Development & Debugging
		{ LogCategory.Debug, "🐞" },
		{ LogCategory.Test, "🧪" },
		{ LogCategory.Profiling, "📊" },
		{ LogCategory.Editor, "🔧" },
		{ LogCategory.Analytics, "📈" },
		{ LogCategory.Performance, "⚡" },

		// System Core
		{ LogCategory.Memory, "🧠" },
		{ LogCategory.Security, "🔒" },
		{ LogCategory.Database, "💾" },
		{ LogCategory.FileIO, "📁" },
		{ LogCategory.Configuration, "⚙️" },
		{ LogCategory.Loading, "⏳" },
		{ LogCategory.SaveSystem, "💾" },
		{ LogCategory.Resources, "📦" },
		{ LogCategory.AssetBundle, "📦" },
		{ LogCategory.SceneManagement, "🏙️" },

		// Rendering & Graphics
		{ LogCategory.Rendering, "🎨" },
		{ LogCategory.Particles, "✨" },
		{ LogCategory.Shaders, "🔆" },
		{ LogCategory.PostProcessing, "🖼️" },
		{ LogCategory.Lighting, "💡" },

		// Game Systems
		{ LogCategory.Gameplay, "🎮" },
		{ LogCategory.Combat, "⚔️" },
		{ LogCategory.Inventory, "🎒" },
		{ LogCategory.Quest, "📜" },
		{ LogCategory.Dialog, "💬" },
		{ LogCategory.LevelGeneration, "🏗️" },
		{ LogCategory.Progression, "📈" },
		{ LogCategory.Achievement, "🏆" },
		{ LogCategory.Economy, "💰" },

		// Input & UI
		{ LogCategory.Input, "🎮" },
		{ LogCategory.UI, "🖥️" },

		// Animation & Physics
		{ LogCategory.Animation, "🏃" },
		{ LogCategory.Physics, "🔮" },
		{ LogCategory.Physics2D, "🎯" },

		// AI & Behavior
		{ LogCategory.AI, "🧠" },

		// Audio
		{ LogCategory.Audio, "🔊" },

		// Networking & Services
		{ LogCategory.Network, "🌐" },
		{ LogCategory.Cloud, "☁️" },
		{ LogCategory.Authentication, "🔑" },
		{ LogCategory.Social, "👥" },
		{ LogCategory.Purchasing, "💲" },

		// Localization
		{ LogCategory.Localization, "🌍" },

		// Other
		{ LogCategory.Camera, "📷" },
		{ LogCategory.Initialization, "🚀" },
		{ LogCategory.Utilities, "🔧" },
		{ LogCategory.UCI, "♟️" }
	};

	/// <summary>
	///     Gets the emoji representation for a specific log category.
	/// </summary>
	/// <param name="category">The log category.</param>
	/// <returns>
	///     Emoji string for the specified category, or <c>❓</c> if the category is not recognized.
	/// </returns>
	public static string GetEmoji(LogCategory category) =>
		CategoryToEmoji.GetValueOrDefault(category, "❓");
}

/// <summary>
///     Provides logging utility methods across different log levels and categories.
/// </summary>
public static class Logger
{
	/// <summary>
	///     Event invoked when a log message is processed.
	/// </summary>
	public static event Action<LogLevel, LogCategory, string, string?>? OnLog;

	/// <summary>
	///     Logs an error message.
	/// </summary>
	/// <param name="message">The log message or data.</param>
	/// <param name="context">The log context (typically the calling object), or <c>null</c>.</param>
	/// <param name="category">The log category, default is <see cref="LogCategory.Default" />.</param>
	[Conditional("DEBUG")]
	public static void LogError(object message, object? context = null, LogCategory category = default) =>
		Log(LogLevel.Error, message, category, context);

	/// <summary>
	///     Logs an error message with string interpolation support.
	/// </summary>
	/// <param name="message">The log message as a <see cref="FormattableString" />.</param>
	/// <param name="context">The log context (typically the calling object), or <c>null</c>.</param>
	/// <param name="category">The log category, default is <see cref="LogCategory.Default" />.</param>
	[Conditional("DEBUG")]
	public static void LogError(
		FormattableString message,
		object?           context  = null,
		LogCategory       category = default) =>
		Log(LogLevel.Error, message, category, context);

	/// <summary>
	///     Logs an exception message.
	/// </summary>
	/// <param name="message">Exception or error message object.</param>
	/// <param name="context">The log context, or <c>null</c>.</param>
	/// <param name="category">The log category, default is <see cref="LogCategory.Default" />.</param>
	[Conditional("DEBUG")]
	public static void LogException(object message, object? context = null, LogCategory category = default) =>
		Log(LogLevel.Exception, message, category, context);

	/// <summary>
	///     Logs an exception message with string interpolation support.
	/// </summary>
	/// <param name="message">Exception message as a <see cref="FormattableString" />.</param>
	/// <param name="context">The log context, or <c>null</c>.</param>
	/// <param name="category">The log category, default is <see cref="LogCategory.Default" />.</param>
	[Conditional("DEBUG")]
	public static void LogException(
		FormattableString message,
		object?           context  = null,
		LogCategory       category = default) =>
		Log(LogLevel.Exception, message, category, context);

	/// <summary>
	///     Logs an informational message.
	/// </summary>
	/// <param name="message">Information message object.</param>
	/// <param name="context">The log context, or <c>null</c>.</param>
	/// <param name="category">The log category, default is <see cref="LogCategory.Default" />.</param>
	[Conditional("DEBUG")]
	public static void LogInfo(object message, object? context = null, LogCategory category = default) =>
		Log(LogLevel.Info, message, category, context);

	/// <summary>
	///     Logs an informational message with formatting.
	/// </summary>
	/// <param name="message">Information message as a <see cref="FormattableString" />.</param>
	/// <param name="context">The log context, or <c>null</c>.</param>
	/// <param name="category">The log category, default is <see cref="LogCategory.Default" />.</param>
	[Conditional("DEBUG")]
	public static void LogInfo(FormattableString message, object? context = null, LogCategory category = default) =>
		Log(LogLevel.Info, message, category, context);

	/// <summary>
	///     Logs a successful operation message.
	/// </summary>
	/// <param name="message">Success message object.</param>
	/// <param name="context">The log context, or <c>null</c>.</param>
	/// <param name="category">The log category, default is <see cref="LogCategory.Default" />.</param>
	[Conditional("DEBUG")]
	public static void LogSuccess(object message, object? context = null, LogCategory category = default) =>
		Log(LogLevel.Success, message, category, context);

	/// <summary>
	///     Logs a successful operation message with formatting.
	/// </summary>
	/// <param name="message">Success message as a <see cref="FormattableString" />.</param>
	/// <param name="context">The log context, or <c>null</c>.</param>
	/// <param name="category">The log category, default is <see cref="LogCategory.Default" />.</param>
	[Conditional("DEBUG")]
	public static void LogSuccess(
		FormattableString message,
		object?           context  = null,
		LogCategory       category = default) =>
		Log(LogLevel.Success, message, category, context);

	/// <summary>
	///     Logs a warning message.
	/// </summary>
	/// <param name="message">Warning message object.</param>
	/// <param name="context">The log context, or <c>null</c>.</param>
	/// <param name="category">The log category, default is <see cref="LogCategory.Default" />.</param>
	[Conditional("DEBUG")]
	public static void LogWarning(object message, object? context = null, LogCategory category = default) =>
		Log(LogLevel.Warning, message, category, context);

	/// <summary>
	///     Logs a warning message with formatting.
	/// </summary>
	/// <param name="message">Warning message as a <see cref="FormattableString" />.</param>
	/// <param name="context">The log context, or <c>null</c>.</param>
	/// <param name="category">The log category, default is <see cref="LogCategory.Default" />.</param>
	[Conditional("DEBUG")]
	public static void LogWarning(
		FormattableString message,
		object?           context  = null,
		LogCategory       category = default) =>
		Log(LogLevel.Warning, message, category, context);

	/// <summary>
	///     Core logging method for object-based messages.
	/// </summary>
	/// <param name="level">The log level.</param>
	/// <param name="message">The message object to log.</param>
	/// <param name="category">The log category.</param>
	/// <param name="context">The context of the log message, or <c>null</c>.</param>
	[Conditional("DEBUG")]
	private static void Log(LogLevel level, object message, LogCategory category, object? context)
	{
		string formattedMessage;

		if (message is IEnumerable collection and not string)
		{
			var collectionAsStrings =
				collection.Cast<object>().Select(o => o?.ToString() ?? "null");

			formattedMessage = $"[{string.Join(", ", collectionAsStrings)}]";
		}
		else
		{
			formattedMessage = message?.ToString() ?? string.Empty;
		}

		string? logContext    = context?.GetType().Name;
		var     categoryToLog = category.ToString() == null ? LogCategory.Default : category;
		OnLog?.Invoke(level, categoryToLog, formattedMessage, logContext);
	}

	/// <summary>
	///     Core logging method for formattable string messages.
	/// </summary>
	/// <param name="level">The log level.</param>
	/// <param name="formattableMessage">The message as a <see cref="FormattableString" />.</param>
	/// <param name="category">The log category.</param>
	/// <param name="context">The context of the log message, or <c>null</c>.</param>
	[Conditional("DEBUG")]
	private static void Log(
		LogLevel          level,
		FormattableString formattableMessage,
		LogCategory       category,
		object?           context)
	{
		object?[] arguments     = formattableMessage.GetArguments();
		var       formattedArgs = new object?[arguments.Length];

		for (var i = 0; i < arguments.Length; i++)
		{
			object? arg = arguments[i];

			if (arg is IEnumerable collection and not string)
			{
				var collectionAsStrings =
					collection.Cast<object>().Select(o => o?.ToString() ?? "null");

				formattedArgs[i] = $"\n[{string.Join("\n", collectionAsStrings)}]";
			}
			else
			{
				formattedArgs[i] = arg;
			}
		}

		var formattedMessage = string.Format(formattableMessage.Format, formattedArgs);

		string? logContext    = context?.GetType().Name;
		var     categoryToLog = category.ToString() == null ? LogCategory.Default : category;
		OnLog?.Invoke(level, categoryToLog, formattedMessage, logContext);
	}
}

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
