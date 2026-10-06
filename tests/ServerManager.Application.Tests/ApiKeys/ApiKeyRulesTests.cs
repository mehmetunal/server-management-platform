using System.Net;
using ServerManager.Application.ApiKeys;

namespace ServerManager.Application.Tests.ApiKeys;

public class ApiKeyRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Effective_permissions_are_intersection_of_scope_and_user_permissions()
    {
        var effective = ApiKeyRules.EffectivePermissions(
            ["server.view", "deployment.execute", "backup.view"],
            ["server.view", "backup.view", "alert.view"]);

        Assert.Equal(["backup.view", "server.view"], effective.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Effective_permissions_are_empty_when_user_lost_everything()
    {
        Assert.Empty(ApiKeyRules.EffectivePermissions(["server.view"], []));
    }

    [Fact]
    public void Lifetime_options_respect_max_and_no_expiry_setting()
    {
        var limited = ApiKeyRules.LifetimeOptions(new ApiKeyOptions { MaxLifetimeDays = 90, AllowNoExpiry = false });
        var open = ApiKeyRules.LifetimeOptions(new ApiKeyOptions { MaxLifetimeDays = 365, AllowNoExpiry = true });

        Assert.Equal([30, 90], limited);
        Assert.Equal([30, 90, 365, null], open);
    }

    [Fact]
    public void Expiry_is_resolved_from_lifetime()
    {
        Assert.True(ApiKeyRules.TryResolveExpiry(30, new ApiKeyOptions(), Now, out var expiresAt, out _));
        Assert.Equal(Now.AddDays(30), expiresAt);
    }

    [Fact]
    public void No_expiry_requires_configuration()
    {
        Assert.False(ApiKeyRules.TryResolveExpiry(null, new ApiKeyOptions { AllowNoExpiry = false }, Now, out _, out var error));
        Assert.NotNull(error);

        Assert.True(ApiKeyRules.TryResolveExpiry(null, new ApiKeyOptions { AllowNoExpiry = true }, Now, out var expiresAt, out _));
        Assert.Null(expiresAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(366)]
    public void Lifetime_outside_limits_is_rejected(int days)
    {
        Assert.False(ApiKeyRules.TryResolveExpiry(days, new ApiKeyOptions { MaxLifetimeDays = 365 }, Now, out _, out _));
    }

    [Fact]
    public void Expiry_check_uses_current_time()
    {
        Assert.False(ApiKeyRules.IsExpired(null, Now));
        Assert.False(ApiKeyRules.IsExpired(Now.AddSeconds(1), Now));
        Assert.True(ApiKeyRules.IsExpired(Now, Now));
        Assert.True(ApiKeyRules.IsExpired(Now.AddDays(-1), Now));
    }

    [Fact]
    public void Empty_allow_list_allows_everyone()
    {
        Assert.True(ApiKeyRules.TryParseAllowList("  ", out var networks, out _));
        Assert.Empty(networks);
        Assert.True(ApiKeyRules.IsIpAllowed(networks, IPAddress.Parse("198.51.100.7")));
    }

    [Theory]
    [InlineData("203.0.113.10", "203.0.113.10", true)]
    [InlineData("203.0.113.10", "203.0.113.11", false)]
    [InlineData("10.0.0.0/8", "10.20.30.40", true)]
    [InlineData("10.0.0.0/8", "11.0.0.1", false)]
    [InlineData("192.168.1.0/24, 10.0.0.0/8", "192.168.1.200", true)]
    [InlineData("2001:db8::/32", "2001:db8::1", true)]
    [InlineData("2001:db8::/32", "10.0.0.1", false)]
    [InlineData("10.0.0.0/8", "::ffff:10.1.2.3", true)]
    public void Ip_allow_list_matches_cidr(string list, string remote, bool expected)
    {
        Assert.True(ApiKeyRules.TryParseAllowList(list, out var networks, out var error), error);
        Assert.Equal(expected, ApiKeyRules.IsIpAllowed(networks, IPAddress.Parse(remote)));
    }

    [Fact]
    public void Unknown_remote_address_is_rejected_when_list_is_set()
    {
        Assert.True(ApiKeyRules.TryParseAllowList("10.0.0.0/8", out var networks, out _));
        Assert.False(ApiKeyRules.IsIpAllowed(networks, null));
    }

    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("10.0.0.0/33")]
    [InlineData("300.1.1.1")]
    public void Invalid_allow_list_entry_is_reported(string list)
    {
        Assert.False(ApiKeyRules.TryParseAllowList(list, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Allow_list_round_trips_through_storage_format()
    {
        Assert.True(ApiKeyRules.TryParseAllowList("203.0.113.10\n10.0.0.0/8", out var networks, out _));
        var stored = ApiKeyRules.FormatAllowList(networks);

        Assert.Equal("203.0.113.10/32,10.0.0.0/8", stored);
        Assert.True(ApiKeyRules.TryParseAllowList(stored, out var reparsed, out _));
        Assert.Equal(networks, reparsed);
    }

    [Fact]
    public void Scopes_round_trip_sorted_and_distinct()
    {
        var stored = ApiKeyRules.FormatScopes(["server.view", "alert.view", "server.view"]);

        Assert.Equal("alert.view server.view", stored);
        Assert.Equal(["alert.view", "server.view"], ApiKeyRules.ParseScopes(stored));
    }
}
