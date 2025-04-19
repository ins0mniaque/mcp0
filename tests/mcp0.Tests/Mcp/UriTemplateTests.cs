using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Routing;

namespace mcp0.Mcp;

[TestClass]
[SuppressMessage("Design", "CA1054:URI parameters should not be strings", Justification = "DataRow requires constants")]
public sealed class UriTemplateTests
{
    [TestMethod]
    [DataRow("http://localhost:8080/{id}", "http://localhost:8080/1")]
    [DataRow("http://localhost:8080/dir/{id}", "http://localhost:8080/dir/1")]
    [DataRow("http://localhost:8080/dir/{id}/{page}", "http://localhost:8080/dir/1/home")]
    [DataRow("http://localhost:8080/dir/{id}/{page}?q={query}", "http://localhost:8080/dir/1/search?q=test")]
    public void MatchesCorrectly(string template, string uri)
    {
        var uriTemplate = new UriTemplate(template);

        Assert.IsTrue(uriTemplate.Match(uri));
    }

    [TestMethod]
    [DataRow("http://localhost:8080/{id}", "http://localhost/1")]
    [DataRow("http://localhost:8080/{id}", "https://localhost:8080/1")]
    [DataRow("http://localhost:8080/{id}", "http://example.com:8080/1")]
    [DataRow("http://localhost:8080/{id}", "http://localhost:8080/1/mismatch")]
    public void MismatchesCorrectly(string template, string uri)
    {
        var uriTemplate = new UriTemplate(template);

        Assert.IsFalse(uriTemplate.Match(uri));
    }

    [TestMethod]
    public void ParsesCorrectly()
    {
        var uriTemplate = new UriTemplate("http://localhost:8080/dir/{id}/{page}?q={query}");
        var values = uriTemplate.Parse("http://localhost:8080/dir/1/search?q=test");

        Assert.IsNotNull(values);
        Assert.AreEqual(3, values.Count);
        Assert.AreEqual("1", values["id"]);
        Assert.AreEqual("search", values["page"]);
        Assert.AreEqual("test", values["query"]);
    }

    [TestMethod]
    public void ExpandsCorrectly()
    {
        var uriTemplate = new UriTemplate("http://localhost:8080/dir/{id}/{page}{?query}");
        var values = new RouteValueDictionary { ["id"] = "1", ["page"] = "search", ["query"] = "test" };

        Assert.AreEqual("http://localhost:8080/dir/1/search?query=test", uriTemplate.Expand(values));
    }

    [TestMethod]
    public void ExpandEscapesCorrectly()
    {
        var uriTemplate = new UriTemplate("http://localhost:8080/dir/{id}/{page}{?query}{#fragment}");
        var values = new RouteValueDictionary { ["id"] = "%\uD83D\uDE03", ["page"] = "100% AND %25", ["query"] = "❤️", ["fragment"] = "100% AND %25 %\uD83D\uDE03" };

        Assert.AreEqual("http://localhost:8080/dir/%25%F0%9F%98%83/100%25%20AND%20%2525?query=%E2%9D%A4%EF%B8%8F#100%25%20AND%20%2525%20%25%F0%9F%98%83", uriTemplate.Expand(values));
    }
}