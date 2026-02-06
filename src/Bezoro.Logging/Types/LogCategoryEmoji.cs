namespace Bezoro.Logging.Types;

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
		{ LogCategory.FileIo, "📁" },
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
		{ LogCategory.Ui, "🖥️" },

		// Animation & Physics
		{ LogCategory.Animation, "🏃" },
		{ LogCategory.Physics, "🔮" },
		{ LogCategory.Physics2D, "🎯" },

		// AI & Behavior
		{ LogCategory.Ai, "🧠" },

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
		{ LogCategory.Uci, "♟️" }
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
