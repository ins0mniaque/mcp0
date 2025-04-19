namespace mcp0.Core;

internal sealed class DisposableTask : IAsyncDisposable
{
    private readonly CancellationTokenSource cts;
    private readonly Task task;

    public DisposableTask(Func<CancellationToken, Task> generateTask, CancellationToken cancellationToken)
    {
        cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        task = generateTask(cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        await cts.CancelAsync();

        try { await task; }
        catch (OperationCanceledException exception) when (exception.CancellationToken == cts.Token) { }
        finally { cts.Dispose(); }
    }
}