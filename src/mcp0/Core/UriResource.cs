using System.Text.RegularExpressions;

using Microsoft.AspNetCore.StaticFiles;

using ModelContextProtocol;
using ModelContextProtocol.Protocol.Types;

namespace mcp0.Core;

internal static partial class UriResource
{
    [GeneratedRegex("data:(?<type>.+?);base64,(?<data>.+)", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GenerateDataUriParser();
    private static readonly Regex dataUriParser = GenerateDataUriParser();

    private static readonly FileExtensionContentTypeProvider mimeTypeProvider = new();

    public static Resource Create(string name, string uri)
    {
        return new()
        {
            Name = name,
            Description = ParseDescription(ref uri),
            Uri = Uri.IsWellFormedUriString(uri, UriKind.Absolute) ? uri : new Uri(uri).AbsoluteUri,
            MimeType = mimeTypeProvider.TryGetContentType(uri, out var mimeType) ? mimeType : null
        };
    }

    private static string? ParseDescription(ref string uri)
    {
        return CommandLine.ParseComment(ref uri);
    }

    public static async Task<(byte[] Data, string? MimeType)> Download(this Resource resource, IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        var uri = new Uri(resource.Uri);

        if (uri.Scheme is "http" or "https")
        {
            using var client = httpClientFactory.CreateClient();
            using var result = await client.GetAsync(uri, cancellationToken);

            if (result.IsSuccessStatusCode)
            {
                var data = await result.Content.ReadAsByteArrayAsync(cancellationToken);
                var mimetype = result.Content.Headers.ContentType?.MediaType;

                return (data, mimetype);
            }

            throw new McpException($"Error: HTTP Status Code {result.StatusCode}: {result.ReasonPhrase}");
        }

        if (uri.IsFile)
        {
            var data = await File.ReadAllBytesAsync(uri.LocalPath, cancellationToken);

            return (data, null);
        }

        if (uri.Scheme is "data")
        {
            var match = dataUriParser.Match(uri.OriginalString);
            var data = Convert.FromBase64String(match.Groups["data"].Value);
            var mimetype = match.Groups["type"].Value;

            return (data, mimetype);
        }

        throw new McpException($"Unsupported resource protocol: {uri}");
    }

    public static async Task<ResourceContents> ToResourceContents(this Resource resource, byte[] data, string? mimetype, CancellationToken cancellationToken)
    {
        var binary = data.AsSpan().Contains((byte)0);
        if (binary)
        {
            return new BlobResourceContents
            {
                Uri = resource.Uri,
                Blob = Convert.ToBase64String(data),
                MimeType = mimetype ?? resource.MimeType ?? "application/octet-stream"
            };
        }

        string text;
        using (var stream = new MemoryStream(data))
        using (var reader = new StreamReader(stream))
            text = await reader.ReadToEndAsync(cancellationToken);

        return new TextResourceContents
        {
            Uri = resource.Uri,
            Text = text,
            MimeType = mimetype ?? resource.MimeType ?? "text/plain"
        };
    }
}