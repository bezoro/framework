using System;
using System.Collections.Generic;
using System.Diagnostics;
using Bezoro.Core.Logging;

namespace Bezoro.Core
{
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
		public static event Action<LogLevel, LogCategory, string, string> OnLog;

		[Conditional("DEBUG")]
		public static void LogError(string message, object? context = null, LogCategory category = default) =>
			Log(LogLevel.Error, message, category, context);

		[Conditional("DEBUG")]
		public static void LogException(string message, object? context = null, LogCategory category = default) =>
			Log(LogLevel.Exception, message, category, context);

		[Conditional("DEBUG")]
		public static void LogInfo(string message, object? context, LogCategory category = default) =>
			Log(LogLevel.Info, message, category, context);

		[Conditional("DEBUG")]
		public static void LogSuccess(string message, object? context = null, LogCategory category = default) =>
			Log(LogLevel.Success, message, category, context);

		[Conditional("DEBUG")]
		public static void LogWarning(string message, object? context = null, LogCategory category = default) =>
			Log(LogLevel.Warning, message, category, context);

		[Conditional("DEBUG")]
		private static void Log(LogLevel level, string message, LogCategory category, object? context)
		{
			string? logContext    = context?.GetType().Name;
			var     categoryToLog = category.ToString() == null ? LogCategory.Default : category;
			OnLog?.Invoke(level, categoryToLog, message, logContext);
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
}
