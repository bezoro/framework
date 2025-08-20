namespace Bezoro.UCI;

internal interface IUciTransportLogger
{
	void LogInfo(string message);
	void LogDebug(string message);
	void LogError(Exception exception, string message);
}
