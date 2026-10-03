using ServerManager.Application.Files;

namespace ServerManager.Application.Tests.Files;

public class PosixAccountParserTests
{
    [Fact]
    public void Maps_ids_to_names_and_skips_comments_and_broken_lines()
    {
        const string passwd = """
            # yorum
            root:x:0:0:root:/root:/bin/ash
            deploy:x:1000:100::/config:/bin/bash
            broken-line
            nobody:x:abc:65534::/:/sbin/nologin
            duplicate:x:1000:100::/:/bin/sh
            :x:5:5::/:/bin/sh
            """;

        var names = PosixAccountParser.Parse(passwd);

        Assert.Equal(2, names.Count);
        Assert.Equal("root", names[0]);
        Assert.Equal("deploy", names[1000]);
    }

    [Fact]
    public void Parses_group_file_format()
    {
        var names = PosixAccountParser.Parse("users:x:100:deploy,ops\nwheel:x:10:root\n");

        Assert.Equal("users", names[100]);
        Assert.Equal("wheel", names[10]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Returns_empty_for_missing_content(string? content)
    {
        Assert.Empty(PosixAccountParser.Parse(content));
    }
}
