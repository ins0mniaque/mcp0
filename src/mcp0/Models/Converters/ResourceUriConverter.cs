using System.Text.Json;
using System.Text.Json.Serialization;

using mcp0.Core;

namespace mcp0.Models.Converters;

internal sealed class ResourceUriConverter : JsonConverter<Uri>
{
    public static string Convert(Uri uri) => uri.IsFile ? Posix.UnexpandPath(uri.LocalPath) : uri.ToString();
    public static Uri Convert(string uri) => new(Posix.ExpandPath(uri), UriKind.Absolute);
    public static Uri? TryConvert(string uri) => Uri.TryCreate(Posix.ExpandPath(uri), UriKind.Absolute, out var result) ? result : null;

    public override Uri Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.GetString() is { } uri)
            return Convert(uri);

        throw new JsonException("Expected a string value for the URI");
    }

    public override void Write(Utf8JsonWriter writer, Uri uri, JsonSerializerOptions options)
    {
        writer.WriteStringValue(Convert(uri));
    }
}