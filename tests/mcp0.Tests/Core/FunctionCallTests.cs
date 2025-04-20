using System.Text.Json;

namespace mcp0.Core;

[TestClass]
public sealed class FunctionCallTests
{
    [TestMethod]
    public void ParsesNonFunctionCallsCorrectly()
    {
        var success = FunctionCall.TryParse("This is not a function call", out _, out _);

        Assert.IsFalse(success);
    }

    [TestMethod]
    public void ParsesInlinedFunctionCallsCorrectly()
    {
        var success = FunctionCall.TryParse("This is function() call is inlined", out _, out _);

        Assert.IsFalse(success);
    }

    [TestMethod]
    public void ParsesFunctionCallsWithZeroArgumentsCorrectly()
    {
        var success = FunctionCall.TryParse("function()", out var function, out var arguments);

        Assert.IsTrue(success);
        Assert.AreEqual("function", function);
        Assert.AreEqual(0, arguments.Length);
    }

    [TestMethod]
    public void ParsesFunctionCallsWithOneArgumentCorrectly()
    {
        var success = FunctionCall.TryParse("function(\"argument\")", out var function, out var arguments);

        Assert.IsTrue(success);
        Assert.AreEqual("function", function);
        Assert.AreEqual(1, arguments.Length);
        Assert.AreEqual(JsonValueKind.String, arguments[0].ValueKind);
    }

    [TestMethod]
    public void ParsesFunctionCallsWithTwoArgumentsCorrectly()
    {
        var success = FunctionCall.TryParse("function(42, true)", out var function, out var arguments);

        Assert.IsTrue(success);
        Assert.AreEqual("function", function);
        Assert.AreEqual(2, arguments.Length);
        Assert.AreEqual(JsonValueKind.Number, arguments[0].ValueKind);
        Assert.AreEqual(JsonValueKind.True, arguments[1].ValueKind);
    }

    [TestMethod]
    public void ParsesFunctionCallsWithComplexArgumentsCorrectly()
    {
        var success = FunctionCall.TryParse("function(null, { \"key\": [ ] })", out var function, out var arguments);

        Assert.IsTrue(success);
        Assert.AreEqual("function", function);
        Assert.AreEqual(2, arguments.Length);
        Assert.AreEqual(JsonValueKind.Null, arguments[0].ValueKind);
        Assert.AreEqual(JsonValueKind.Object, arguments[1].ValueKind);
        Assert.IsTrue(arguments[1].TryGetProperty("key", out var element));
        Assert.AreEqual(JsonValueKind.Array, element.ValueKind);
    }

    [TestMethod]
    public void ParsesFunctionCallsWithSpacesCorrectly()
    {
        var success = FunctionCall.TryParse("  function  (  42  ,  true  )  ", out var function, out var arguments);

        Assert.IsTrue(success);
        Assert.AreEqual("function", function);
        Assert.AreEqual(2, arguments.Length);
        Assert.AreEqual(JsonValueKind.Number, arguments[0].ValueKind);
        Assert.AreEqual(JsonValueKind.True, arguments[1].ValueKind);
    }
}