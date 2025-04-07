using System.Text.Json;

namespace mcp0.Tests;

[TestClass]
public sealed class JsonSchemaTests
{
    [TestMethod]
    [DataRow("""{"type": null}""", "null")]
    [DataRow("""{"type": "symbol"}""", "symbol")]
    public void ParsesSymbolsCorrectly(string json, string symbol)
    {
        var actual = JsonSchema.Parse(JsonDocument.Parse(json).RootElement.GetProperty("type"));
        var expected = new JsonSchemaSymbol(symbol);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("""{"type": "boolean"}""", "boolean")]
    [DataRow("""{"type": "number"}""", "number")]
    [DataRow("""{"type": "string"}""", "string")]
    [DataRow("""{"type": "integer"}""", "integer")]
    public void ParsesPrimitiveTypesCorrectly(string json, string type)
    {
        var actual = JsonSchema.Parse(JsonDocument.Parse(json).RootElement.GetProperty("type"));
        var expected = new JsonSchemaPrimitiveType(type, true);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ParsesArrayTypeCorrectly()
    {
        var json = """{ "type": "array", "items": { "type": "string" } }""";

        var actual = JsonSchema.Parse(JsonDocument.Parse(json).RootElement);
        var expected = new JsonSchemaArrayType(new JsonSchemaPrimitiveType("string", true), true);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ParsesObjectTypeCorrectly()
    {
        var json =
        """
        {
            "type": "object",
            "properties": {
              "propString": {
               "type": "string"
              },
              "propInteger": {
               "type": "integer"
              }
            },
            "required": ["propString"]
        }
        """;

        var actual = JsonSchema.Parse(JsonDocument.Parse(json).RootElement);
        var expected = new JsonSchemaObjectType(
        [
            new JsonSchemaProperty("propString", new JsonSchemaPrimitiveType("string", true)),
            new JsonSchemaProperty("propInteger", new JsonSchemaPrimitiveType("integer", false))
        ], true);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ParsesEnumTypeCorrectly()
    {
        var json = """{ "enum": ["a", "b", "c"] }""";

        var actual = JsonSchema.Parse(JsonDocument.Parse(json).RootElement);
        var expected = new JsonSchemaUnionType(
        [
            new JsonSchemaSymbol("a"),
            new JsonSchemaSymbol("b"),
            new JsonSchemaSymbol("c")
        ], true);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ParsesConstTypeCorrectly()
    {
        var json = """{ "const": "a" }""";

        var actual = JsonSchema.Parse(JsonDocument.Parse(json).RootElement);
        var expected = new JsonSchemaUnionType([new JsonSchemaSymbol("a")], true);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ParsesAnyOfTypeCorrectly()
    {
        var json = """{ "anyOf": ["string", "number", "null"] }""";

        var actual = JsonSchema.Parse(JsonDocument.Parse(json).RootElement);
        var expected = new JsonSchemaUnionType(
        [
            new JsonSchemaPrimitiveType("string", true),
            new JsonSchemaPrimitiveType("number", true),
            new JsonSchemaSymbol("null")
        ], true);

        Assert.AreEqual(expected, actual);
    }
}
