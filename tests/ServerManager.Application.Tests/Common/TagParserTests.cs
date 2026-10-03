using ServerManager.Application.Common;

namespace ServerManager.Application.Tests.Common;

public class TagParserTests
{
    [Fact]
    public void Parse_trims_lowercases_and_deduplicates()
    {
        var tags = TagParser.Parse(" Production, web;WEB\neu ,, ");

        Assert.Equal(["production", "web", "eu"], tags);
    }

    [Fact]
    public void Parse_returns_empty_for_null()
    {
        Assert.Empty(TagParser.Parse(null));
    }
}
