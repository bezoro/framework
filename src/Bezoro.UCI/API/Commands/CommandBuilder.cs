using System.Collections.Generic;

namespace Bezoro.UCI.API.Commands
{
	/// <summary>
	///     A fluent builder for creating command sequences
	/// </summary>
	public class CommandBuilder
	{
		private readonly List<IEngineCommand> _commands = new();

		/// <summary>
		///     Sends a text command to the engine
		/// </summary>
		/// <param name="text">The text to send</param>
		/// <returns>The builder for chaining</returns>
		public CommandBuilder Send(string text)
		{
			_commands.Add(new SendTextCommand(text));
			return this;
		}

		/// <summary>
		///     Waits for a specific token from the engine output
		/// </summary>
		/// <param name="token">The token to wait for</param>
		/// <returns>The builder for chaining</returns>
		public CommandBuilder WaitFor(string token)
		{
			_commands.Add(new WaitForTokenCommand(token));
			return this;
		}

		/// <summary>
		///     Builds a composite command that executes all commands in sequence
		///     and returns the result of the last command
		/// </summary>
		/// <returns>The composite command</returns>
		public IEngineCommand<TResult> Build<TResult>() => new CompositeCommand<TResult>(_commands.ToArray());
	}
}
