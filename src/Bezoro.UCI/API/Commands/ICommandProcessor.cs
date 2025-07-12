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
		Task<T> ProcessCommandAsync<T>(IEngineCommand command);

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
