namespace mcp0.Core;

internal static class TypeAlias
{
    public static string ToJsonSchema(string? type) => type switch
    {
        "bool" => "boolean",
        "num" => "number",
        "str" => "string",
        "int" => "integer",
        null => "string",
        _ => type
    };

    public static string FromJsonSchema(string? type) => type switch
    {
        "boolean" => "bool",
        "number" => "num",
        "string" => "str",
        "integer" => "int",
        null => "str",
        _ => type
    };
}