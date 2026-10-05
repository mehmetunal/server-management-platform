using System.Net;
using ServerManager.Application.Authorization;
using ServerManager.Infrastructure.Identity;
using ServerManager.Web.Tests.Infrastructure;

namespace ServerManager.Web.Tests;

[Collection(WebCollection.Name)]
public sealed class LoginProtectionTests(ServerManagerWebFactory factory)
{
    // RateLimiting: "login" politikası IP başına dakikada 10 istek.
    private const int LoginPermitPerMinute = 10;

    [Fact]
    public async Task Repeated_wrong_passwords_throttle_the_account_even_for_correct_password_without_identity_lockout()
    {
        using var client = factory.CreateTestClient();
        var email = await factory.CreateUserAsync(Roles.Viewer);
        var token = await client.GetAntiforgeryTokenAsync("/Account/Login", TestContext.Current.CancellationToken);

        for (var i = 0; i < LoginAttemptThrottle.FreeAttempts; i++)
        {
            using var failed = await client.PostLoginAsync(email, "Wrong-Password-123", TestContext.Current.CancellationToken, token);
            Assert.NotEqual(HttpStatusCode.OK, failed.StatusCode);
        }

        // Bekleme süresi dolmadan doğru parola da reddedilir; Identity kilit sayacı ise ilerlemez.
        using var throttled = await client.PostLoginAsync(email, ServerManagerWebFactory.DefaultUserPassword, TestContext.Current.CancellationToken, token);
        var body = await throttled.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.NotEqual(HttpStatusCode.Redirect, throttled.StatusCode);
        Assert.Contains("saniye sonra tekrar deneyin", body, StringComparison.OrdinalIgnoreCase);
        Assert.False(await factory.IsLockedOutAsync(email));
    }

    [Fact]
    public async Task Login_endpoint_is_rate_limited_per_client_ip()
    {
        using var client = factory.CreateTestClient();
        var token = await client.GetAntiforgeryTokenAsync("/Account/Login", TestContext.Current.CancellationToken);

        for (var i = 0; i < LoginPermitPerMinute; i++)
        {
            using var attempt = await client.PostLoginAsync("nobody@webtests.local", "Wrong-Password-123", TestContext.Current.CancellationToken, token);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, attempt.StatusCode);
        }

        using var limited = await client.PostLoginAsync("nobody@webtests.local", "Wrong-Password-123", TestContext.Current.CancellationToken, token);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);

        // Başka bir IP'den gelen istemci etkilenmez.
        using var otherClient = factory.CreateTestClient();
        using var other = await otherClient.PostLoginAsync("nobody@webtests.local", "Wrong-Password-123", TestContext.Current.CancellationToken);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, other.StatusCode);
    }
}
