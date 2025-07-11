using System.Threading.Tasks;
using Bezoro.Core.Common.Extensions;

namespace Bezoro.UCI.API.Commands
{
	/// <summary>
	///     Command for waiting for a specific token from the engine
	/// </summary>
	public readonly record struct WaitForTokenCommand : IEngineCommand
	{
		private readonly string _token;

		public WaitForTokenCommand(string token)
		{
			_token = token;
		}

		public async Task<object> ExecuteAsync(UCIEngine engine)
		{
			Logger.LogInfo($"Waiting for token: {_token.Bold()}", this, LogCategory.UCI);
			string result = await engine.WaitForTokenAsync(_token);
			Logger.LogSuccess($"Received {_token.Bold()} -> {result}", this, LogCategory.UCI);
			return result;
		}
	}
}
