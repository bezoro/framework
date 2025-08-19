using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Bezoro.UCI;

internal interface IUciTransport : IAsyncDisposable
{
	IAsyncEnumerable<string> ReadLinesAsync(uint timeoutSec, CancellationToken ct);
	Task                     StartAsync(CancellationToken ct);
	Task                     WriteLineAsync(string line, CancellationToken ct);

	event Action<int?, string?>? Exited;
}
