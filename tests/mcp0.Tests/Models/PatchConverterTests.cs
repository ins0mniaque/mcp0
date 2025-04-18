using System.Text.Json;

namespace mcp0.Models;

[TestClass]
public sealed class PatchConverterTests
{
    [TestMethod]
    public void DeserializesRemovePatchFromNullCorrectly()
    {
        var json = "null";

        var actual = JsonSerializer.Deserialize(json, ModelContext.Default.Patch);
        var expected = Patch.Remove;

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void DeserializesRemovePatchCorrectly()
    {
        var json = "false";

        var actual = JsonSerializer.Deserialize(json, ModelContext.Default.Patch);
        var expected = Patch.Remove;

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void DeserializesNamePatchCorrectly()
    {
        var json = "\"new-name\"";

        var actual = JsonSerializer.Deserialize(json, ModelContext.Default.Patch);
        var expected = new Patch { Name = "new-name" };

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void DeserializesDescriptionPatchCorrectly()
    {
        var json = "\"# New description\"";

        var actual = JsonSerializer.Deserialize(json, ModelContext.Default.Patch);
        var expected = new Patch { Description = "New description" };

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void DeserializesNameAndDescriptionPatchCorrectly()
    {
        var json = "\"new-name  # New description\"";

        var actual = JsonSerializer.Deserialize(json, ModelContext.Default.Patch);
        var expected = new Patch
        {
            Name = "new-name",
            Description = "New description"
        };

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void DeserializesObjectPatchCorrectly()
    {
        var json = "{ \"name\": \"new-name\", \"description\": \"New description\" }";

        var actual = JsonSerializer.Deserialize(json, ModelContext.Default.Patch);
        var expected = new Patch
        {
            Name = "new-name",
            Description = "New description"
        };

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void SerializesRemovePatchToFalseCorrectly()
    {
        var expected = Patch.Remove;

        var json = JsonSerializer.Serialize(expected, ModelContext.Default.Patch);

        Assert.AreEqual(json, "false");
    }

    [TestMethod]
    public void SerializesRemovePatchCorrectly()
    {
        var expected = Patch.Remove;

        var json = JsonSerializer.Serialize(expected, ModelContext.Default.Patch);
        var actual = JsonSerializer.Deserialize(json, ModelContext.Default.Patch);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void SerializesNamePatchCorrectly()
    {
        var expected = new Patch { Name = "new-name" };

        var json = JsonSerializer.Serialize(expected, ModelContext.Default.Patch);
        var actual = JsonSerializer.Deserialize(json, ModelContext.Default.Patch);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void SerializesDescriptionPatchCorrectly()
    {
        var expected = new Patch { Description = "New description" };

        var json = JsonSerializer.Serialize(expected, ModelContext.Default.Patch);
        var actual = JsonSerializer.Deserialize(json, ModelContext.Default.Patch);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void SerializesNameAndDescriptionPatchCorrectly()
    {
        var expected = new Patch
        {
            Name = "new-name",
            Description = "New description"
        };

        var json = JsonSerializer.Serialize(expected, ModelContext.Default.Patch);
        var actual = JsonSerializer.Deserialize(json, ModelContext.Default.Patch);

        Assert.AreEqual(expected, actual);
    }
}