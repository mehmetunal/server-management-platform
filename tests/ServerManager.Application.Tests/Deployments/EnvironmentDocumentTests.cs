using ServerManager.Application.Deployments;

namespace ServerManager.Application.Tests.Deployments;

public class EnvironmentDocumentTests
{
    private const string Sample = "# veritabanı\nDATABASE_URL=postgres://u:p@db/app?x=1\n\n# api\nAPI_KEY=abc=def\nEMPTY=\n";

    private static EnvironmentDocument Parse(string text)
    {
        Assert.True(EnvironmentDocument.TryParse(text, out var document, out var error), error);
        return document;
    }

    [Fact]
    public void Parse_keeps_keys_values_and_order()
    {
        var document = Parse(Sample);

        Assert.Equal(["DATABASE_URL", "API_KEY", "EMPTY"], document.Keys);
        Assert.Equal("postgres://u:p@db/app?x=1", document.GetValue("DATABASE_URL"));
        Assert.Equal("abc=def", document.GetValue("API_KEY"));
        Assert.Equal(string.Empty, document.GetValue("EMPTY"));
        Assert.Null(document.GetValue("MISSING"));
    }

    [Fact]
    public void Unchanged_document_serializes_back_to_the_same_text()
    {
        Assert.Equal(Sample, Parse(Sample).ToString());
    }

    [Fact]
    public void Updating_a_value_keeps_comments_order_and_other_values()
    {
        var document = Parse(Sample);

        Assert.False(document.Set("API_KEY", "yeni"));

        Assert.Equal("# veritabanı\nDATABASE_URL=postgres://u:p@db/app?x=1\n\n# api\nAPI_KEY=yeni\nEMPTY=\n", document.ToString());
    }

    [Fact]
    public void New_key_is_appended_before_trailing_blank_lines()
    {
        var document = Parse("A=1\n# son\n\n\n");

        Assert.True(document.Set("B", "2"));

        Assert.Equal("A=1\n# son\nB=2\n", document.ToString());
    }

    [Fact]
    public void Removing_a_key_deletes_only_its_line()
    {
        var document = Parse(Sample);

        Assert.True(document.Remove("DATABASE_URL"));
        Assert.False(document.Remove("DATABASE_URL"));

        Assert.Equal("# veritabanı\n\n# api\nAPI_KEY=abc=def\nEMPTY=\n", document.ToString());
    }

    [Fact]
    public void Merge_without_overwrite_skips_existing_keys()
    {
        var document = Parse("A=1\nB=2\n");

        var result = document.Merge([new("B", "x"), new("C", "3")], overwrite: false);

        Assert.Equal(["C"], result.Added);
        Assert.Empty(result.Updated);
        Assert.Equal(["B"], result.Skipped);
        Assert.Equal("A=1\nB=2\nC=3\n", document.ToString());
    }

    [Fact]
    public void Merge_with_overwrite_updates_changed_values_only()
    {
        var document = Parse("A=1\nB=2\n");

        var result = document.Merge([new("A", "1"), new("B", "x")], overwrite: true);

        Assert.Empty(result.Added);
        Assert.Equal(["B"], result.Updated);
        Assert.Equal("A=1\nB=x\n", document.ToString());
    }

    [Fact]
    public void Empty_document_serializes_to_an_empty_file()
    {
        var document = Parse("A=1\n");
        document.Remove("A");

        Assert.Equal("\n", document.ToString());
        Assert.Equal(0, document.Count);
    }

    [Theory]
    [InlineData("A=1\nA=2")]
    [InlineData("1BAD=x")]
    public void Invalid_content_is_rejected(string text) =>
        Assert.False(EnvironmentDocument.TryParse(text, out _, out _));

    [Theory]
    [InlineData("API_KEY", true)]
    [InlineData("_x1", true)]
    [InlineData("1A", false)]
    [InlineData("A-B", false)]
    [InlineData("A=B", false)]
    [InlineData("", false)]
    public void Key_rule_matches_env_file(string key, bool valid) =>
        Assert.Equal(valid, EnvironmentDocument.IsValidKey(key));

    [Theory]
    [InlineData("tek satır", true)]
    [InlineData("", true)]
    [InlineData("iki\nsatır", false)]
    [InlineData("sonda boşluk ", false)]
    public void Values_must_be_single_line(string value, bool valid) =>
        Assert.Equal(valid, EnvironmentDocument.TryValidateValue(value, out _));
}
