using System.Diagnostics.CodeAnalysis;

namespace mcp0.Mcp;

[TestClass]
[SuppressMessage("Design", "CA1054:URI parameters should not be strings", Justification = "DataRow requires constants")]
public sealed class UriTemplateMatcherTests
{
    [TestMethod]
    [DataRow("http://localhost:8080/{id}", "http://localhost:8080/1")]
    [DataRow("http://localhost:8080/dir/{id}", "http://localhost:8080/dir/1")]
    [DataRow("http://localhost:8080/dir/{id}/{page}", "http://localhost:8080/dir/1/home")]
    [DataRow("http://localhost:8080/dir/{id}/{page}?q={query}", "http://localhost:8080/dir/1/search?q=test")]
    public void MatchesCorrectly(string uriTemplate, string uri)
    {
        var matcher = new UriTemplateMatcher(new Uri(uriTemplate, UriKind.Absolute));

        Assert.IsTrue(matcher.Match(new Uri(uri, UriKind.Absolute)));
    }

    [TestMethod]
    [DataRow("http://localhost:8080/{id}", "http://localhost/1")]
    [DataRow("http://localhost:8080/{id}", "https://localhost:8080/1")]
    [DataRow("http://localhost:8080/{id}", "http://example.com:8080/1")]
    [DataRow("http://localhost:8080/{id}", "http://localhost:8080/1/mismatch")]
    public void MismatchesCorrectly(string uriTemplate, string uri)
    {
        var matcher = new UriTemplateMatcher(new Uri(uriTemplate, UriKind.Absolute));

        Assert.IsFalse(matcher.Match(new Uri(uri, UriKind.Absolute)));
    }
}