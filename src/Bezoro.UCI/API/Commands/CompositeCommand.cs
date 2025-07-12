using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bezoro.UCI.API.Commands
{
	/// <summary>
	///     A composite command that executes multiple commands in sequence
	/// </summary>
	public class CompositeCommand : IEngineCommand
	{
		private readonly List<IEngineCommand> _commands;

		/// <summary>
		///     Creates a new composite command
		/// </summary>
		/// <param name="commands">The commands to execute in sequence</param>
		public CompositeCommand(params IEngineCommand[] commands)
		{
			_commands = new List<IEngineCommand>(commands);
		}

		/// <summary>
		///     Adds a command to the sequence
		/// </summary>
		/// <param name="command">The command to add</param>
		public CompositeCommand Add(IEngineCommand command)
		{
			_commands.Add(command);
			return this;
		}

		/// <summary>
		///     Executes all commands in sequence and returns the result of the last command
		/// </summary>
		/// <param name="engine">The UCI engine to execute against</param>
		/// <returns>The result of the last command in the sequence</returns>
		public async Task<object?> ExecuteAsync(UCIEngine engine)
		{
			Logger.LogInfo("Executing composite command with " + _commands.Count + " steps", this, LogCategory.UCI);

			object? result = null;
			foreach (var command in _commands)
			{
				result = await command.ExecuteAsync(engine).ConfigureAwait(false);
			}

			return result;
		}
	}
}
