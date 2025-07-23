#if NETSTANDARD2_1
namespace System.Threading.Tasks;

/// <summary>
/// Poly-fill for Task/Task&lt;T&gt;.WaitAsync that exists on .NET 6+
/// but not on netstandard2.1.
/// </summary>
internal static class TaskWaitExtensions
{
    public static async Task WaitAsync(this Task task, CancellationToken ct)
    {
        if (task.IsCompleted)
        {
            await task.ConfigureAwait(false);
            return;
        }

        // arrange cancellation monitoring
        var tcs = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await using var _ = ct.Register(static s =>
                ((TaskCompletionSource<object?>)s!).TrySetResult(null), tcs);

        // whichever completes first
        if (await Task.WhenAny(task, tcs.Task).ConfigureAwait(false) != task)
            ct.ThrowIfCancellationRequested();

        // propagate exceptions / result
        await task.ConfigureAwait(false);
    }

    public static async Task<T> WaitAsync<T>(this Task<T> task, CancellationToken ct)
    {
        await ((Task)task).WaitAsync(ct).ConfigureAwait(false);
        return await task.ConfigureAwait(false);
    }
}
#endif
