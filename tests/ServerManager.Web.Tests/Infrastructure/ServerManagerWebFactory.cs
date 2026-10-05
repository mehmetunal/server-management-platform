using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServerManager.Infrastructure.Identity;

namespace ServerManager.Web.Tests.Infrastructure;

/// <summary>
/// Uygulamayı üretim ayarlarıyla (Development dışı ortam, Secure cookie, HTTPS) bellek içi test sunucusunda açar.
/// Arka plan işçileri kapatılır; her istemciye ayrı bir istemci IP'si verilir ki IP bazlı hız sınırları testleri etkilemesin.
/// </summary>
public class ServerManagerWebFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@webtests.local";
    public const string AdminPassword = "WebTests-Admin-2026";
    public const string DefaultUserPassword = "WebTests-User-2026";

    private static int _clientCounter;

    private readonly string _databaseName = "SmWebTests_" + Guid.NewGuid().ToString("N")[..16];
    private readonly string _logDirectory = Path.Combine(Path.GetTempPath(), "sm-web-tests", Guid.NewGuid().ToString("N"));
    private bool _hostCreated;

    protected virtual bool TwoFactorRequired => false;

    /// <summary>Test çıktısını yönlendirmeyen, cookie taşıyan ve kendi istemci IP'si olan bir HTTPS istemcisi döner. SM_TEST_SQL yoksa testi atlar.</summary>
    public HttpClient CreateTestClient()
    {
        Assert.SkipUnless(TestDatabase.IsConfigured, TestDatabase.SkipReason);

        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });

        var counter = Interlocked.Increment(ref _clientCounter);
        var address = new IPAddress([10, 77, (byte)(counter / 250 % 250), (byte)(counter % 250 + 1)]);
        client.DefaultRequestHeaders.Add(TestClientIpStartupFilter.HeaderName, address.ToString());
        return client;
    }

    public async Task<string> CreateUserAsync(string role, bool twoFactorEnabled = false)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"{role.ToLowerInvariant()}-{Guid.NewGuid():N}@webtests.local";
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = $"Test {role}",
            IsActive = true,
            TwoFactorEnabled = twoFactorEnabled
        };

        var created = await userManager.CreateAsync(user, DefaultUserPassword);
        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Description)));
        var added = await userManager.AddToRoleAsync(user, role);
        Assert.True(added.Succeeded, string.Join(", ", added.Errors.Select(e => e.Description)));
        return email;
    }

    /// <summary>Yalnızca verilen yetkilere sahip yeni bir rol ve bu roldeki kullanıcıyı oluşturur.</summary>
    public async Task<string> CreateUserWithPermissionsAsync(params string[] permissions)
    {
        var roleName = "Test-" + Guid.NewGuid().ToString("N")[..12];
        using (var scope = Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var role = new ApplicationRole(roleName);
            var created = await roleManager.CreateAsync(role);
            Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Description)));
            foreach (var permission in permissions)
                await roleManager.AddClaimAsync(role, new System.Security.Claims.Claim(Application.Authorization.Permissions.ClaimType, permission));
        }

        return await CreateUserAsync(roleName);
    }

    public async Task<bool> IsLockedOutAsync(string email)
    {
        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        return user is not null && await userManager.IsLockedOutAsync(user);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:DefaultConnection", TestDatabase.ConnectionStringFor(_databaseName));
        builder.UseSetting("Database:AutoCreate", "true");
        builder.UseSetting("Security:MasterKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("TwoFactor:Required", TwoFactorRequired ? "true" : "false");
        builder.UseSetting("Monitoring:Enabled", "false");
        builder.UseSetting("Alerting:Enabled", "false");
        builder.UseSetting("Backup:Enabled", "false");
        builder.UseSetting("Serilog:WriteTo:1:Args:path", Path.Combine(_logDirectory, "server-manager-.log"));
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");

        builder.ConfigureTestServices(services =>
        {
            // Uygulamanın kendi arka plan işçileri (izleme, yedek zamanlayıcı, bulut senkronu...) testlerde çalışmaz.
            var workers = services
                .Where(d => d.ServiceType == typeof(IHostedService)
                    && d.ImplementationType?.Assembly.GetName().Name?.StartsWith("ServerManager.", StringComparison.Ordinal) == true)
                .ToList();
            foreach (var worker in workers)
                services.Remove(worker);

            services.AddSingleton<IStartupFilter, TestClientIpStartupFilter>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        _hostCreated = true;
        return base.CreateHost(builder);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        if (_hostCreated)
            await TestDatabase.DropAsync(_databaseName);

        try
        {
            if (Directory.Exists(_logDirectory))
                Directory.Delete(_logDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Serilog dosyayı geç bırakırsa geçici log klasörü kalabilir; testi etkilemez.
        }

        GC.SuppressFinalize(this);
    }
}

/// <summary>TwoFactor:Required açık bir uygulama örneği.</summary>
public sealed class TwoFactorRequiredWebFactory : ServerManagerWebFactory
{
    protected override bool TwoFactorRequired => true;
}

[CollectionDefinition(Name)]
public sealed class WebCollection : ICollectionFixture<ServerManagerWebFactory>, ICollectionFixture<TwoFactorRequiredWebFactory>
{
    public const string Name = "Web";
}
