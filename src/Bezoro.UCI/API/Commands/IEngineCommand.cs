using System.Threading.Tasks;

namespace Bezoro.UCI.API.Commands
{
	/// <summary>
	///     Interface for all commands that can be sent to the UCI engine
	/// </summary>
	public interface IEngineCommand
	{
		/// <summary>
		///     Executes the command against the engine
		/// </summary>
		/// <param name="connector">The engine connector to execute the command against</param>
		/// <returns>The command result</returns>
		Task<object> ExecuteAsync(UCIEngine engine);
	}
}
