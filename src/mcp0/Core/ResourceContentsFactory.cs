using ModelContextProtocol.Protocol.Types;

namespace mcp0.Core;

internal static class ResourceContentsFactory
{
    public static async Task<ResourceContents> ToResourceContents(this Resource resource, byte[] data, CancellationToken cancellationToken)
    {
        var binary = data.AsSpan().Contains((byte)0);
        if (binary)
            return new BlobResourceContents
            {
                Uri = resource.Uri,
                Blob = Convert.ToBase64String(data),
                MimeType = resource.MimeType
            };

        string text;
        using (var stream = new MemoryStream(data))
        using (var reader = new StreamReader(stream))
            text = await reader.ReadToEndAsync(cancellationToken);

        return new TextResourceContents
        {
            Uri = resource.Uri,
            Text = text,
            MimeType = resource.MimeType
        };
    }
}