using System.Threading.Tasks;
using Bezoro.Core.Common.Extensions;

namespace Bezoro.UCI.API.Commands
{
	/// <summary>
	///     Command for sending raw text to the engine
	/// </summary>
	public readonly record struct SendTextCommand : IEngineCommand<object?>
	{
		private readonly string _command;

		public SendTextCommand(string command)
		{
			_command = command;
		}

		public async Task<object?> ExecuteAsync(UCIEngine engine)
		{
			Logger.LogInfo($"Sending Command: {_command.Bold()}", this, LogCategory.UCI);

			await engine.WriteLineAsync(_command);
			Logger.LogSuccess($"Command {_command.Bold()} Sent", this, LogCategory.UCI);

			return null;
		}
	}
}
