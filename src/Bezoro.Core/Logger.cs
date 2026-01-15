using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
///     Provides a clean, flexible logging utility with optional complexity.
/// </summary>
public static class Logger
{
	/// <summary>
	///     Event invoked when a log message is processed.
	/// </summary>
	public static event Action<LogPayload>? OnLog;

	/// <summary>
	///     Logs a message with optional complexity.
	/// </summary>
	/// <param name="message">The message to log (string, number, object, FormattableString, etc.).</param>
	/// <param name="level">The severity level (default: Info).</param>
	/// <param name="category">Optional log category.</param>
	/// <param name="contextObject">Optional context object (e.g., Unity Object for console highlighting).</param>
	/// <param name="captureCallerInfo">Whether to automatically capture caller information.</param>
	/// <param name="memberName">
	///     Automatically populated with the calling member name via <see cref="CallerMemberNameAttribute" />.
	///     Do not provide this parameter manually.
	/// </param>
	/// <param name="filePath">
	///     Automatically populated with the source file path via <see cref="CallerFilePathAttribute" />.
	///     Do not provide this parameter manually.
	/// </param>
	[Conditional("DEBUG")]
	public static void Log(
		object                     message,
		LogLevel                   level             = LogLevel.Info,
		LogCategory?               category          = null,
		object?                    contextObject     = null,
		bool                       captureCallerInfo = false,
		[CallerMemberName] string? memberName        = null,
		[CallerFilePath]   string? filePath          = null)
	{
		// Capture caller info if requested
		string? callerInfo = null;
		if (captureCallerInfo && memberName != null)
		{
			string typeName = ExtractTypeNameFromFilePath(filePath);
			callerInfo = $"{typeName}.{memberName}()";
		}

		// Format the message based on type
		string formattedMessage = message is FormattableString formattable
									  ? FormatMessage(formattable)
									  : FormatMessage(message);

		BuildAndInvokePayload(
			formattedMessage,
			level,
			category,
			contextObject,
			null,
			callerInfo);
	}

	/// <summary>
	///     Logs an exception with automatic detail extraction.
	/// </summary>
	/// <param name="exception">The exception to log.</param>
	/// <param name="category">Optional log category.</param>
	/// <param name="contextObject">Optional context object (e.g., Unity Object for console highlighting).</param>
	/// <param name="captureCallerInfo">Whether to automatically capture caller information (default: true).</param>
	/// <param name="memberName">
	///     Automatically populated with the calling member name via <see cref="CallerMemberNameAttribute" />.
	///     Do not provide this parameter manually.
	/// </param>
	/// <param name="filePath">
	///     Automatically populated with the source file path via <see cref="CallerFilePathAttribute" />.
	///     Do not provide this parameter manually.
	/// </param>
	[Conditional("DEBUG")]
	public static void Log(
		Exception                  exception,
		LogCategory?               category          = null,
		object?                    contextObject     = null,
		bool                       captureCallerInfo = true,
		[CallerMemberName] string? memberName        = null,
		[CallerFilePath]   string? filePath          = null)
	{
		string message       = exception.Message;
		string exceptionType = exception.GetType().Name;

		// Capture caller info if requested
		string? callerInfo = null;
		if (captureCallerInfo && memberName != null)
		{
			string typeName = ExtractTypeNameFromFilePath(filePath);
			callerInfo = $"{typeName}.{memberName}()";
		}

		BuildAndInvokePayload(
			message,
			LogLevel.Exception,
			category,
			contextObject,
			exceptionType,
			callerInfo);
	}

	/// <summary>
	///     Extracts the type name from a file path (e.g., "GameManager" from "path/to/GameManager.cs").
	/// </summary>
	private static string ExtractTypeNameFromFilePath(string? filePath)
	{
		if (string.IsNullOrEmpty(filePath))
			return "Unknown";

		string? fileName = Path.GetFileNameWithoutExtension(filePath);
		return fileName ?? "Unknown";
	}

	/// <summary>
	///     Formats a message object into a string representation.
	/// </summary>
	private static string FormatMessage(object message)
	{
		if (message is not (IEnumerable collection and not string)) return message.ToString() ?? string.Empty;

		var collectionAsStrings =
			collection.Cast<object>().Select(o => o?.ToString() ?? "null");

		return $"[{string.Join(", ", collectionAsStrings)}]";
	}

	/// <summary>
	///     Formats a formattable string message with proper collection handling.
	/// </summary>
	private static string FormatMessage(FormattableString formattableMessage)
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

		return string.Format(formattableMessage.Format, formattedArgs);
	}

	/// <summary>
	///     Builds the log payload and invokes the OnLog event.
	/// </summary>
	private static void BuildAndInvokePayload(
		string       message,
		LogLevel     level,
		LogCategory? category,
		object?      contextObject,
		string?      exceptionType,
		string?      callerInfo)
	{
		string severityEmoji = LogLevelEmoji.GetEmoji(level);
		string? categoryEmoji = category.HasValue
									? LogCategoryEmoji.GetEmoji(category.Value)
									: null;

		// Build formatted message: [severity] [category] [ExceptionType ::] Message [:: Caller]
		string formattedMessage = severityEmoji;

		if (categoryEmoji != null)
			formattedMessage += $" [{categoryEmoji}]";

		if (exceptionType != null)
			formattedMessage += $" {exceptionType} ::";

		formattedMessage += $" {message}";

		if (callerInfo != null)
			formattedMessage += $" :: {callerInfo}";

		var payload = new LogPayload
		{
			Level            = level,
			Category         = category,
			Message          = message,
			SeverityEmoji    = severityEmoji,
			CategoryEmoji    = categoryEmoji,
			ExceptionType    = exceptionType,
			CallerInfo       = callerInfo,
			FormattedMessage = formattedMessage,
			ContextObject    = contextObject
		};

		OnLog?.Invoke(payload);
	}
}

/// <summary>
///     Provides utility methods to get emoji representations for log severity levels.
/// </summary>
public static class LogLevelEmoji
{
	private static readonly Dictionary<LogLevel, string> LevelToEmoji = new()
	{
		{ LogLevel.Info, "ℹ️" },
		{ LogLevel.Success, "✅" },
		{ LogLevel.Warning, "⚠️" },
		{ LogLevel.Error, "❌" },
		{ LogLevel.Exception, "💥" }
	};

	/// <summary>
	///     Gets the emoji representation for a specific log level.
	/// </summary>
	/// <param name="level">The log level.</param>
	/// <returns>
	///     Emoji string for the specified level, or <c>ℹ️</c> if not recognized.
	/// </returns>
	public static string GetEmoji(LogLevel level) =>
		LevelToEmoji.GetValueOrDefault(level, "ℹ️");
}

/// <summary>
///     Contains all data for a single log event.
/// </summary>
public sealed class LogPayload
{
	/// <summary>
	///     Optional category for the log message.
	/// </summary>
	public LogCategory? Category { get; init; }

	/// <summary>
	///     The severity level of the log.
	/// </summary>
	public required LogLevel Level { get; init; }

	/// <summary>
	///     Optional context object (e.g., Unity Object for console highlighting).
	/// </summary>
	public object? ContextObject { get; init; }

	/// <summary>
	///     Fully formatted log message ready for output.
	/// </summary>
	public required string FormattedMessage { get; init; }

	/// <summary>
	///     The raw message content.
	/// </summary>
	public required string Message { get; init; }

	/// <summary>
	///     Emoji representing the severity level.
	/// </summary>
	public required string SeverityEmoji { get; init; }

	/// <summary>
	///     Caller information in format "TypeName.MethodName()" (if captured).
	/// </summary>
	public string? CallerInfo { get; init; }

	/// <summary>
	///     Emoji representing the category (if category is specified).
	/// </summary>
	public string? CategoryEmoji { get; init; }

	/// <summary>
	///     Exception type name (only for exceptions).
	/// </summary>
	public string? ExceptionType { get; init; }
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
