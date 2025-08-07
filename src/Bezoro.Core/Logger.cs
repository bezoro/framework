using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Bezoro.Core.Logging;

namespace Bezoro.Core;

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
	///     Gets the emoji representation for a specific log category
	/// </summary>
	/// <param name="category">The log category</param>
	/// <returns>Emoji string for the specified category</returns>
	public static string GetEmoji(LogCategory category) =>
		CategoryToEmoji.GetValueOrDefault(category, "❓");
}

public static class Logger
{
	public static event Action<LogLevel, LogCategory, string, string?> OnLog;

	[Conditional("DEBUG")]
	public static void LogError(object message, object? context = null, LogCategory category = default) =>
		Log(LogLevel.Error, message, category, context);

	[Conditional("DEBUG")]
	public static void LogError(
		FormattableString message,
		object?           context  = null,
		LogCategory       category = default) =>
		Log(LogLevel.Error, message, category, context);

	[Conditional("DEBUG")]
	public static void LogException(object message, object? context = null, LogCategory category = default) =>
		Log(LogLevel.Exception, message, category, context);

	[Conditional("DEBUG")]
	public static void LogException(
		FormattableString message,
		object?           context  = null,
		LogCategory       category = default) =>
		Log(LogLevel.Exception, message, category, context);

	[Conditional("DEBUG")]
	public static void LogInfo(object message, object? context = null, LogCategory category = default) =>
		Log(LogLevel.Info, message, category, context);

	[Conditional("DEBUG")]
	public static void LogInfo(FormattableString message, object? context = null, LogCategory category = default) =>
		Log(LogLevel.Info, message, category, context);

	[Conditional("DEBUG")]
	public static void LogSuccess(object message, object? context = null, LogCategory category = default) =>
		Log(LogLevel.Success, message, category, context);

	[Conditional("DEBUG")]
	public static void LogSuccess(
		FormattableString message,
		object?           context  = null,
		LogCategory       category = default) =>
		Log(LogLevel.Success, message, category, context);

	[Conditional("DEBUG")]
	public static void LogWarning(object message, object? context = null, LogCategory category = default) =>
		Log(LogLevel.Warning, message, category, context);

	[Conditional("DEBUG")]
	public static void LogWarning(
		FormattableString message,
		object?           context  = null,
		LogCategory       category = default) =>
		Log(LogLevel.Warning, message, category, context);

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
			formattedMessage = message?.ToString() ?? string.Empty;

		string? logContext    = context?.GetType().Name;
		var     categoryToLog = category.ToString() == null ? LogCategory.Default : category;
		OnLog?.Invoke(level, categoryToLog, formattedMessage, logContext);
	}

	[Conditional("DEBUG")]
	private static void Log(
		LogLevel          level,
		FormattableString formattableMessage,
		LogCategory       category,
		object?           context)
	{
		object[] arguments     = formattableMessage.GetArguments();
		var      formattedArgs = new object[arguments.Length];

		for (var i = 0; i < arguments.Length; i++)
		{
			object arg = arguments[i];

			if (arg is IEnumerable collection and not string)
			{
				var collectionAsStrings =
					collection.Cast<object>().Select(o => o?.ToString() ?? "null");

				formattedArgs[i] = $"\n[{string.Join("\n", collectionAsStrings)}]";
			}
			else
				formattedArgs[i] = arg;
		}

		var formattedMessage = string.Format(formattableMessage.Format, formattedArgs);

		string? logContext    = context?.GetType().Name;
		var     categoryToLog = category.ToString() == null ? LogCategory.Default : category;
		OnLog?.Invoke(level, categoryToLog, formattedMessage, logContext);
	}
}

public enum LogCategory
{
	// Core
	Default,
	System,
	Other,

	// Development & Debugging
	Debug,
	Test,
	Profiling,
	Editor,
	Analytics,
	Performance,

	// System Core
	Memory,
	Security,
	Database,
	FileIO,
	Configuration,
	Loading,
	SaveSystem,
	Resources,
	AssetBundle,
	SceneManagement,

	// Rendering & Graphics
	Rendering,
	Particles,
	Shaders,
	PostProcessing,
	Lighting,

	// Game Systems
	Gameplay,
	Combat,
	Inventory,
	Quest,
	Dialog,
	LevelGeneration,
	Progression,
	Achievement,
	Economy,

	// Input & UI
	Input,
	UI,

	// Animation & Physics
	Animation,
	Physics,
	Physics2D,

	// AI & Behavior
	AI,

	// Audio
	Audio,

	// Networking & Services
	Network,
	Cloud,
	Authentication,
	Social,
	Purchasing,

	// Localization
	Localization,

	// Other
	Camera,
	Initialization,
	Utilities,
	UCI
}
