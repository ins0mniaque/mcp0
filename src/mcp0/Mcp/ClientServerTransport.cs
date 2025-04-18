using System.IO.Pipelines;

using Microsoft.Extensions.Logging;

using ModelContextProtocol.Protocol.Transport;

namespace mcp0.Mcp;

internal sealed class ClientServerTransport : IAsyncDisposable
{
    private readonly Pipe clientToServerPipe = new();
    private readonly Pipe serverToClientPipe = new();
    private readonly StreamClientTransport clientTransport;
    private readonly StreamServerTransport serverTransport;

    public ClientServerTransport(string? serverName = null, ILoggerFactory? loggerFactory = null)
    {
        clientTransport = new StreamClientTransport(
            clientToServerPipe.Writer.AsStream(),
            serverToClientPipe.Reader.AsStream(),
            loggerFactory);

        serverTransport = new StreamServerTransport(
            clientToServerPipe.Reader.AsStream(),
            serverToClientPipe.Writer.AsStream(),
            serverName,
            loggerFactory);
    }

    public IClientTransport ClientTransport => clientTransport;
    public ITransport ServerTransport => serverTransport;

    public ValueTask DisposeAsync() => serverTransport.DisposeAsync();
}