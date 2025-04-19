namespace mcp0.Core;

internal sealed class CompositeDisposable<T> : List<T>, IDisposable where T : IDisposable
{
    public CompositeDisposable() { }
    public CompositeDisposable(IEnumerable<T> collection) : base(collection) { }
    public CompositeDisposable(int capacity) : base(capacity) { }

    public void Dispose()
    {
        foreach (var disposable in this)
            disposable.Dispose();

        Clear();
    }
}