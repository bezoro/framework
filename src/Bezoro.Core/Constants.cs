namespace Bezoro.Core
{
	public static class Constants
	{
		public static class GameInitialization
		{
			public const int    CONFIG_ASSET_MENU_ORDER     = 0;
			public const string CONFIG_ASSET_MENU_FILE_NAME = "Game_Initialization_Config";
			public const string CONFIG_ASSET_MENU_PATH =
				BEZORO + SLASH + RUNTIME + SLASH + CONFIG + SLASH + "Game Initialization";
		}

		#region Initialization Orders

		public const int DEFAULT_ASSET_MENU_ORDER = 0;
		public const int LAST_INIT_ORDER          = int.MaxValue;
		public const int MANAGER_INIT_ORDER       = int.MinValue;
		public const int REGULAR_INIT_ORDER       = 0;

		#endregion

		#region Special Characters

		public const string AMPERSAND        = "&";
		public const string AND              = "&";
		public const string ASTERISK         = "*";
		public const string AT               = "@";
		public const string BACKSLASH        = "\\";
		public const string BACKTICK         = "`";
		public const string CARET            = "^";
		public const string COLON            = ":";
		public const string COMMA            = ",";
		public const string DOLLAR_SIGN      = "$";
		public const string DOT              = ".";
		public const string EMPTY            = "";
		public const string EQUALS           = "=";
		public const string EXCLAMATION_MARK = "!";
		public const string HASH             = "#";
		public const string HYPHEN           = "-";
		public const string NEWLINE          = "\n";
		public const string PERCENT          = "%";
		public const string PIPE             = "|";
		public const string QUESTION_MARK    = "?";
		public const string QUOTE            = "\"";
		public const string SEMICOLON        = ";";
		public const string SINGLE_QUOTE     = "'";
		public const string SLASH            = "/";
		public const string SPACE            = " ";
		public const string TAB              = "\t";
		public const string TILDE            = "~";
		public const string UNDERSCORE       = "_";

		#endregion

		#region Path and Directory Names

		public const string ASSETS              = "Assets";
		public const string BEZORO              = "Bezoro";
		public const string PROJECT_FOLDER_NAME = "_Project";
		public const string RESOURCES           = "Resources";
		public const string RUNTIME             = "Runtime";
		public const string PROJECT_SETTINGS    = "ProjectSettings";

		#endregion

		#region Asset Categories

		public const string ANIMATION = "Animation";
		public const string AUDIO     = "Audio";
		public const string CONFIG    = "Config";
		public const string DATA      = "Data";
		public const string DEBUG     = "Debug";
		public const string DEVELOP   = "Develop";
		public const string EDITOR    = "Editor";
		public const string FONT      = "Font";
		public const string GAME      = "Game";
		public const string MATERIAL  = "Material";
		public const string MODEL     = "Model";
		public const string SCENE     = "Scene";
		public const string SETTINGS  = "Settings";
		public const string SHADER    = "Shader";
		public const string TEXTURE   = "Texture";

		#endregion
	}
}
