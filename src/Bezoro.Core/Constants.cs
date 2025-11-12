namespace Bezoro.Core;

/// <summary>
/// Provides global constants for directory names, asset categories, menu ordering, and common
/// character strings throughout the Bezoro ecosystem.
/// </summary>
public static class Constants
{
	/// <summary>
	/// Constants related to game initialization configuration assets.
	/// </summary>
	public static class GameInitialization
	{
		/// <summary>
		/// The default ordering for game initialization config assets in asset menus.
		/// </summary>
		public const int CONFIG_ASSET_MENU_ORDER = 0;
		/// <summary>
		/// The file name for the game initialization config asset.
		/// </summary>
		public const string CONFIG_ASSET_MENU_FILE_NAME = "Game_Initialization_Config";
		/// <summary>
		/// The project path for the game initialization config asset.
		/// </summary>
		public const string CONFIG_ASSET_MENU_PATH =
			BEZORO + SLASH + RUNTIME + SLASH + CONFIG + SLASH + "Game Initialization";
	}

	#region Initialization Orders

	/// <summary>
	/// The default ordering for assets in menus.
	/// </summary>
	public const int DEFAULT_ASSET_MENU_ORDER = 0;
	/// <summary>
	/// The order for the last initialization step.
	/// </summary>
	public const int LAST_INIT_ORDER = int.MaxValue;
	/// <summary>
	/// The order for manager initialization.
	/// </summary>
	public const int MANAGER_INIT_ORDER = int.MinValue;
	/// <summary>
	/// The regular/default initialization order.
	/// </summary>
	public const int REGULAR_INIT_ORDER = 0;

	#endregion

	#region Special Characters

	/// <summary>
	/// The ampersand character: <c>&amp;</c>
	/// </summary>
	public const string AMPERSAND = "&";

	/// <summary>
	/// Alias for ampersand (<see cref="AMPERSAND"/>)
	/// </summary>
	public const string AND = "&";

	/// <summary>
	/// The asterisk character: <c>*</c>
	/// </summary>
	public const string ASTERISK = "*";

	/// <summary>
	/// The at sign character: <c>@</c>
	/// </summary>
	public const string AT = "@";

	/// <summary>
	/// The backslash character: <c>\</c>
	/// </summary>
	public const string BACKSLASH = "\\";

	/// <summary>
	/// The backtick character: <c>`</c>
	/// </summary>
	public const string BACKTICK = "`";

	/// <summary>
	/// The caret character: <c>^</c>
	/// </summary>
	public const string CARET = "^";

	/// <summary>
	/// The colon character: <c>:</c>
	/// </summary>
	public const string COLON = ":";

	/// <summary>
	/// The comma character: <c>,</c>
	/// </summary>
	public const string COMMA = ",";

	/// <summary>
	/// The dollar sign character: <c>$</c>
	/// </summary>
	public const string DOLLAR_SIGN = "$";

	/// <summary>
	/// The dot character: <c>.</c>
	/// </summary>
	public const string DOT = ".";

	/// <summary>
	/// An empty string.
	/// </summary>
	public const string EMPTY = "";

	/// <summary>
	/// The equals character: <c>=</c>
	/// </summary>
	public const string EQUALS = "=";

	/// <summary>
	/// The exclamation mark character: <c>!</c>
	/// </summary>
	public const string EXCLAMATION_MARK = "!";

	/// <summary>
	/// The hash character: <c>#</c>
	/// </summary>
	public const string HASH = "#";

	/// <summary>
	/// The hyphen character: <c>-</c>
	/// </summary>
	public const string HYPHEN = "-";

	/// <summary>
	/// The newline literal: <c>\n</c>
	/// </summary>
	public const string NEWLINE = "\n";

	/// <summary>
	/// The percent character: <c>%</c>
	/// </summary>
	public const string PERCENT = "%";

	/// <summary>
	/// The pipe character: <c>|</c>
	/// </summary>
	public const string PIPE = "|";

	/// <summary>
	/// The question mark character: <c>?</c>
	/// </summary>
	public const string QUESTION_MARK = "?";

	/// <summary>
	/// The double quote character: <c>"</c>
	/// </summary>
	public const string QUOTE = "\"";

	/// <summary>
	/// The semicolon character: <c>;</c>
	/// </summary>
	public const string SEMICOLON = ";";

	/// <summary>
	/// The single quote character: <c>'</c>
	/// </summary>
	public const string SINGLE_QUOTE = "'";

	/// <summary>
	/// The slash character: <c>/</c>
	/// </summary>
	public const string SLASH = "/";

	/// <summary>
	/// The space character: <c> </c> (space)
	/// </summary>
	public const string SPACE = " ";

	/// <summary>
	/// The tab literal: <c>\t</c>
	/// </summary>
	public const string TAB = "\t";

	/// <summary>
	/// The tilde character: <c>~</c>
	/// </summary>
	public const string TILDE = "~";

	/// <summary>
	/// The underscore character: <c>_</c>
	/// </summary>
	public const string UNDERSCORE = "_";

	#endregion

	#region Path and Directory Names

	/// <summary>
	/// The <c>Assets</c> project directory.
	/// </summary>
	public const string ASSETS = "Assets";

	/// <summary>
	/// The root <c>Bezoro</c> directory name.
	/// </summary>
	public const string BEZORO = "Bezoro";

	/// <summary>
	/// The <c>_Project</c> folder for project-wide settings.
	/// </summary>
	public const string PROJECT_FOLDER_NAME = "_Project";

	/// <summary>
	/// The <c>Resources</c> folder for resource assets.
	/// </summary>
	public const string RESOURCES = "Resources";

	/// <summary>
	/// The <c>Runtime</c> directory, typically for runtime-specific assets.
	/// </summary>
	public const string RUNTIME = "Runtime";

	/// <summary>
	/// The <c>ProjectSettings</c> directory.
	/// </summary>
	public const string PROJECT_SETTINGS = "ProjectSettings";

	#endregion

	#region Asset Categories

	/// <summary>
	/// The animation asset category.
	/// </summary>
	public const string ANIMATION = "Animation";

	/// <summary>
	/// The audio asset category.
	/// </summary>
	public const string AUDIO = "Audio";

	/// <summary>
	/// The config asset category.
	/// </summary>
	public const string CONFIG = "Config";

	/// <summary>
	/// The data asset category.
	/// </summary>
	public const string DATA = "Data";

	/// <summary>
	/// The debug asset category.
	/// </summary>
	public const string DEBUG = "Debug";

	/// <summary>
	/// The development asset category.
	/// </summary>
	public const string DEVELOP = "Develop";

	/// <summary>
	/// The editor asset category.
	/// </summary>
	public const string EDITOR = "Editor";

	/// <summary>
	/// The font asset category.
	/// </summary>
	public const string FONT = "Font";

	/// <summary>
	/// The game asset category.
	/// </summary>
	public const string GAME = "Game";

	/// <summary>
	/// The material asset category.
	/// </summary>
	public const string MATERIAL = "Material";

	/// <summary>
	/// The model asset category.
	/// </summary>
	public const string MODEL = "Model";

	/// <summary>
	/// The scene asset category.
	/// </summary>
	public const string SCENE = "Scene";

	/// <summary>
	/// The settings asset category.
	/// </summary>
	public const string SETTINGS = "Settings";

	/// <summary>
	/// The shader asset category.
	/// </summary>
	public const string SHADER = "Shader";

	/// <summary>
	/// The texture asset category.
	/// </summary>
	public const string TEXTURE = "Texture";

	#endregion
}
