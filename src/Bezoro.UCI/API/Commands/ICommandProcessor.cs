using System.Threading.Tasks;
using Bezoro.UCI.API.Commands;

namespace Bezoro.UCI.API
{
	/// <summary>
	///     Defines a processor that handles engine commands sequentially
	/// </summary>
	public interface ICommandProcessor
	{
		/// <summary>
		///     Processes a command and returns its result
		/// </summary>
		/// <typeparam name="T">The expected result type</typeparam>
		/// <param name="command">The command to process</param>
		/// <returns>The command result</returns>
		Task<T> ProcessCommandWithResultAsync<T>(IEngineCommand<T> command);

		/// <summary>
		///     Processes a command that does not return a value
		/// </summary>
		/// <param name="command">The command to process</param>
		Task ProcessCommandAsync(IEngineCommand command);

		/// <summary>
		///     Processes a typed command that does not return a value
		/// </summary>
		/// <param name="command">The command to process</param>
		Task ProcessCommandAsync<T>(IEngineCommand<T> command);

		/// <summary>
		///     Creates a new command builder for constructing command sequences
		/// </summary>
		/// <returns>A command builder</returns>
		CommandBuilder CreateCommand();

		/// <summary>
		///     Starts the command processor
		/// </summary>
		Task StartAsync();

		/// <summary>
		///     Stops the command processor
		/// </summary>
		Task StopAsync();
	}
}
