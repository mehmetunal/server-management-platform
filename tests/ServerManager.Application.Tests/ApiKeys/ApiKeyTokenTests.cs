using ServerManager.Application.ApiKeys;

namespace ServerManager.Application.Tests.ApiKeys;

public class ApiKeyTokenTests
{
    [Fact]
    public void Generated_token_has_expected_format_and_hash()
    {
        var key = ApiKeyToken.Generate();

        Assert.Matches("^smk_[a-z0-9]{12}_[A-Za-z0-9]{43}$", key.Token);
        Assert.Equal(key.Token.Substring(4, 12), key.Prefix);
        Assert.Equal(64, key.Hash.Length);
        Assert.Equal(ApiKeyToken.ComputeHash(key.Token), key.Hash);
        Assert.DoesNotContain(key.Token[17..], key.Hash, StringComparison.Ordinal);
    }

    [Fact]
    public void Generated_tokens_are_unique()
    {
        var tokens = Enumerable.Range(0, 200).Select(_ => ApiKeyToken.Generate().Token).ToHashSet();
        Assert.Equal(200, tokens.Count);
    }

    [Fact]
    public void Parse_returns_prefix_for_valid_token()
    {
        var key = ApiKeyToken.Generate();

        Assert.True(ApiKeyToken.TryParse(key.Token, out var prefix));
        Assert.Equal(key.Prefix, prefix);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("smk_abc")]
    [InlineData("xyz_abcdefghijkl_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopq")]
    [InlineData("smk_ABCDEFGHIJKL_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopq")]
    [InlineData("smk_abcdefghijkl-ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopq")]
    [InlineData("smk_abcdefghijkl_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnop_")]
    public void Parse_rejects_malformed_tokens(string? token)
    {
        Assert.False(ApiKeyToken.TryParse(token, out _));
    }

    [Fact]
    public void Verify_accepts_only_the_original_token()
    {
        var key = ApiKeyToken.Generate();
        var other = ApiKeyToken.Generate();
        var tampered = key.Token[..^1] + (key.Token[^1] == 'a' ? 'b' : 'a');

        Assert.True(ApiKeyToken.Verify(key.Token, key.Hash));
        Assert.False(ApiKeyToken.Verify(other.Token, key.Hash));
        Assert.False(ApiKeyToken.Verify(tampered, key.Hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void Verify_rejects_invalid_stored_hash(string storedHash)
    {
        Assert.False(ApiKeyToken.Verify(ApiKeyToken.Generate().Token, storedHash));
    }
}
