namespace Bezoro.UCI.API.Types
{
	public record UCIOption(string Name, string Type, string Default, string Min, string Max, string[] Vars);
}
