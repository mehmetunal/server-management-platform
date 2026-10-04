using ServerManager.Application.Agent;

namespace ServerManager.Application.Tests.Agent;

public class AgentTokensTests
{
    [Fact]
    public void Generate_returns_unique_well_formed_tokens()
    {
        var first = AgentTokens.Generate();
        var second = AgentTokens.Generate();

        Assert.NotEqual(first, second);
        Assert.True(AgentTokens.IsWellFormed(first));
        Assert.StartsWith(AgentTokens.Prefix, first);
    }

    [Fact]
    public void Hash_is_stable_lowercase_sha256_hex()
    {
        var hash = AgentTokens.Hash("sma_test");

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, AgentTokens.Hash("sma_test"));
        Assert.Equal(hash.ToLowerInvariant(), hash);
        Assert.NotEqual(hash, AgentTokens.Hash("sma_tesT"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sma_short")]
    [InlineData("abc_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("sma_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'")]
    public void IsWellFormed_rejects_malformed_tokens(string? token)
    {
        Assert.False(AgentTokens.IsWellFormed(token));
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData(" 2.1.0-beta+3 ", "2.1.0-beta+3")]
    [InlineData("1.0; rm -rf", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizeVersion_accepts_only_safe_versions(string? input, string? expected)
    {
        Assert.Equal(expected, AgentRules.NormalizeVersion(input));
    }
}
