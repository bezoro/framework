using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bezoro.UCI.API.Commands
{
	/// <summary>
	///     A command that executes multiple commands in sequence and returns all results
	/// </summary>
	/// <typeparam name="T">The type of the final result</typeparam>
	public class SequentialCommand<T> : IEngineCommand<T>
	{
		private readonly List<IEngineCommand>            _commands = new();
		private readonly Func<IReadOnlyList<object?>, T> _resultSelector;

		/// <summary>
		///     Creates a new sequential command
		/// </summary>
		/// <param name="commands">The commands to execute</param>
		/// <param name="resultSelector">A function to transform the results of all commands into the final result</param>
		public SequentialCommand(IEnumerable<IEngineCommand> commands, Func<IReadOnlyList<object?>, T> resultSelector)
		{
			_commands.AddRange(commands);
			_resultSelector = resultSelector;
		}

		/// <summary>
		///     Executes all commands in sequence and applies the result selector to their results
		/// </summary>
		/// <param name="engine">The UCI engine to execute against</param>
		/// <returns>The transformed result</returns>
		public async Task<T?> ExecuteAsync(UCIEngine engine)
		{
			Logger.LogInfo("Executing sequential command with " + _commands.Count + " steps", this, LogCategory.UCI);

			var results = new List<object?>(_commands.Count);
			foreach (var command in _commands)
			{
				// Use dynamic outside of pattern matching
				dynamic dynamicCommand = command;
				object? result         = await dynamicCommand.ExecuteAsync(engine).ConfigureAwait(false);
				results.Add(result);
			}

			return _resultSelector(results);
		}
	}
}
