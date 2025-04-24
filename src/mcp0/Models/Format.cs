using System.Buffers;

namespace mcp0.Models;

internal static class Format
{
    public static readonly SearchValues<char> FormattableNameChars = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-");
}