namespace Bezoro.UCI.API
{
	/// <summary>
	///     Factory for creating command processors
	/// </summary>
	public static class CommandProcessorFactory
	{
		/// <summary>
		///     Creates a command processor for the given engine
		/// </summary>
		/// <param name="engine">The UCI engine</param>
		/// <returns>A new command processor</returns>
		public static ICommandProcessor Create(UCIEngine engine) =>
			new CommandProcessor(engine);
	}
}
