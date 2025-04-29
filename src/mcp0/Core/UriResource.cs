using System.Net.Mime;
using System.Text.RegularExpressions;

using Microsoft.AspNetCore.StaticFiles;

using ModelContextProtocol;
using ModelContextProtocol.Protocol.Types;

namespace mcp0.Core;

internal sealed partial class UriResource
{
    [GeneratedRegex("data:(?<type>.+?);base64,(?<data>.+)", RegexOptions.Compiled, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GenerateDataUriParser();
    private static readonly Regex dataUriParser = GenerateDataUriParser();

    private static readonly FileExtensionContentTypeProvider mimeTypeProvider = new();

    public UriResource(Models.Resource resource)
    {
        Uri = resource.Uri;
        Resource = new()
        {
            Name = resource.Name,
            Description = resource.Description,
            Uri = resource.Uri.AbsoluteUri,
            MimeType = resource.MimeType ?? (mimeTypeProvider.TryGetContentType(resource.Uri.AbsoluteUri, out var mimeType) ? mimeType : null)
        };
    }

    public Uri Uri { get; }
    public Resource Resource { get; }

    public async Task<(byte[] Data, string? MimeType)> Download(IHttpClientFactory httpClientFactory, CancellationToken cancellationToken)
    {
        if (Uri.Scheme is "http" or "https")
        {
            using var client = httpClientFactory.CreateClient();
            using var result = await client.GetAsync(Uri, cancellationToken);

            if (result.IsSuccessStatusCode)
            {
                var data = await result.Content.ReadAsByteArrayAsync(cancellationToken);
                var mimetype = result.Content.Headers.ContentType?.MediaType;

                return (data, mimetype);
            }

            throw new McpException($"Error: HTTP Status Code {result.StatusCode}: {result.ReasonPhrase}");
        }

        if (Uri.IsFile)
        {
            var data = await File.ReadAllBytesAsync(Uri.LocalPath, cancellationToken);

            return (data, null);
        }

        if (Uri.Scheme is "data")
        {
            var match = dataUriParser.Match(Uri.AbsoluteUri);
            var data = Convert.FromBase64String(match.Groups["data"].Value);
            var mimetype = match.Groups["type"].Value;

            return (data, mimetype);
        }

        throw new McpException($"Unsupported resource protocol: {Uri}");
    }

    public async Task<ResourceContents> ToResourceContents(byte[] data, string? mimetype, CancellationToken cancellationToken)
    {
        var binary = data.AsSpan().Contains((byte)0);
        if (binary)
        {
            return new BlobResourceContents
            {
                Uri = Resource.Uri,
                Blob = Convert.ToBase64String(data),
                MimeType = mimetype ?? Resource.MimeType ?? MediaTypeNames.Application.Octet
            };
        }

        string text;
        using (var stream = new MemoryStream(data))
        using (var reader = new StreamReader(stream))
            text = await reader.ReadToEndAsync(cancellationToken);

        return new TextResourceContents
        {
            Uri = Resource.Uri,
            Text = text,
            MimeType = mimetype ?? Resource.MimeType ?? MediaTypeNames.Text.Plain
        };
    }
}