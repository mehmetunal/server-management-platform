using System.Net;
using ServerManager.Web.Tests.Infrastructure;

namespace ServerManager.Web.Tests;

/// <summary>TwoFactor:Required açıkken iki adımlı doğrulaması olmayan kullanıcı yalnızca hesap sayfalarına erişebilir.</summary>
[Collection(WebCollection.Name)]
public sealed class TwoFactorEnforcementTests(TwoFactorRequiredWebFactory factory)
{
    [Fact]
    public async Task User_without_two_factor_is_redirected_to_security_setup()
    {
        using var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, TestContext.Current.CancellationToken);

        using var response = await client.GetAsync("/Servers", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Security", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task User_without_two_factor_gets_403_for_ajax_request()
    {
        using var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, TestContext.Current.CancellationToken);
        using var request = HttpClientAuthExtensions.JsonGet("/Servers");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Security_setup_page_stays_reachable()
    {
        using var client = factory.CreateTestClient();
        await client.LoginAsync(ServerManagerWebFactory.AdminEmail, ServerManagerWebFactory.AdminPassword, TestContext.Current.CancellationToken);

        using var response = await client.GetAsync("/Account/Security", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
