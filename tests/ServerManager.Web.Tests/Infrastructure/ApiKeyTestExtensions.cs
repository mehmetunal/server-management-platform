using System.Net.Http.Headers;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ServerManager.Application.DTOs.ApiKeys;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Infrastructure.Identity;
using ServerManager.Web.Services;

namespace ServerManager.Web.Tests.Infrastructure;

public static class ApiKeyTestExtensions
{
    /// <summary>Kullanıcı adına (panelden oluşturulmuş gibi) API anahtarı oluşturur ve tam anahtarı döner.</summary>
    public static async Task<string> CreateApiKeyAsync<TEntry>(
        this WebApplicationFactory<TEntry> factory,
        string email,
        string[] scopes,
        int? lifetimeDays = 30,
        string? allowedIps = null)
        where TEntry : class
    {
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email);
        Assert.NotNull(user);

        using (CurrentUserService.RunAs(user.Id.ToString(), email, "127.0.0.1"))
        {
            var result = await scope.ServiceProvider.GetRequiredService<IApiKeyService>().CreateAsync(new CreateApiKeyDto
            {
                Name = "test-" + Guid.NewGuid().ToString("N")[..6],
                LifetimeDays = lifetimeDays,
                Scopes = scopes.ToList(),
                AllowedIps = allowedIps
            });
            Assert.True(result.IsSuccess, result.Errors.FirstOrDefault()?.Message ?? result.Message);
            return result.Data!.Token;
        }
    }

    public static HttpRequestMessage ApiRequest(HttpMethod method, string path, string? token)
    {
        var request = new HttpRequestMessage(method, path);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
