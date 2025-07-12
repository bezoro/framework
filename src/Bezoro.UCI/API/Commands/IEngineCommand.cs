using System.Threading.Tasks;

namespace Bezoro.UCI.API.Commands
{
	/// <summary>
	///     Base interface for all commands that can be sent to the UCI engine
	/// </summary>
	public interface IEngineCommand { }

	/// <summary>
	///     Interface for commands that return a specific result type
	/// </summary>
	/// <typeparam name="TResult">The type of result returned by the command</typeparam>
	public interface IEngineCommand<TResult> : IEngineCommand
	{
		/// <summary>
		///     Executes the command against the engine
		/// </summary>
		/// <param name="engine">The UCI engine to execute the command against</param>
		/// <returns>The command result</returns>
		Task<TResult?> ExecuteAsync(UCIEngine engine);
	}
}
