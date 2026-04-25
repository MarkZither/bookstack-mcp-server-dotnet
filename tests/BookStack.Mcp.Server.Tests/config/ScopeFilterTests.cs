using BookStack.Mcp.Server.Config;
using FluentAssertions;

namespace BookStack.Mcp.Server.Tests.Config;

public sealed class ScopeFilterTests
{
    [Test]
    public void MatchesScope_EmptyScope_ReturnsFalse()
    {
        ScopeFilter.MatchesScope(1, "my-book", []).Should().BeFalse();
    }

    [Test]
    public void MatchesScope_MatchesById()
    {
        ScopeFilter.MatchesScope(42, "any-slug", ["42"]).Should().BeTrue();
    }

    [Test]
    public void MatchesScope_MatchesBySlug_CaseInsensitive()
    {
        ScopeFilter.MatchesScope(1, "My-Book", ["my-book"]).Should().BeTrue();
    }

    [Test]
    public void MatchesScope_SlugComparison_IgnoresCase()
    {
        ScopeFilter.MatchesScope(1, "MY-BOOK", ["my-book"]).Should().BeTrue();
    }

    [Test]
    public void MatchesScope_NoMatch_ReturnsFalse()
    {
        ScopeFilter.MatchesScope(1, "book-a", ["2", "book-b"]).Should().BeFalse();
    }

    [Test]
    public void MatchesScope_MultipleEntries_MatchesCorrectId()
    {
        ScopeFilter.MatchesScope(5, "slug-5", ["1", "2", "5"]).Should().BeTrue();
    }

    [Test]
    public void MatchesScope_IntegerIdDoesNotMatchSlugNumerically()
    {
        // "42" in scope should only match id==42, not slug=="42-something"
        ScopeFilter.MatchesScope(1, "42", ["42"]).Should().BeTrue(); // slug match
        ScopeFilter.MatchesScope(42, "other", ["42"]).Should().BeTrue(); // id match
        ScopeFilter.MatchesScope(1, "nope", ["42"]).Should().BeFalse(); // neither
    }
}
