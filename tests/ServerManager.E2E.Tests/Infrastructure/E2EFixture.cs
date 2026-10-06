using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.DTOs.Servers;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.ManagedServices;
using ServerManager.Domain.Enums;
using ServerManager.Infrastructure.Identity;
using ServerManager.Web.Services;

namespace ServerManager.E2E.Tests.Infrastructure;

/// <summary>
/// Koleksiyon boyunca tek uygulama örneği, testin kendi SSH kanalı ve panele eklenmiş (host key'i sabitlenmiş) E2E sunucusu.
/// Ortam tanımlı değilse hiçbir şey başlatılmaz; testler <see cref="RequireAsync"/> ile atlanır.
/// </summary>
public sealed class E2EFixture : IAsyncLifetime
{
    public const string ClientIp = "127.0.0.1";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public E2EAppFactory Factory { get; private set; } = null!;

    public TestShell Shell { get; private set; } = null!;

    public Guid ServerId { get; private set; }

    public string ServerName { get; private set; } = string.Empty;

    public string AdminUserId { get; private set; } = string.Empty;

    public ServiceActor ServiceActor => new(AdminUserId, E2EAppFactory.AdminEmail, ClientIp);

    public BackupActor BackupActor => new(AdminUserId, E2EAppFactory.AdminEmail, ClientIp);

    public DeploymentActor DeploymentActor => new(AdminUserId, E2EAppFactory.AdminEmail, ClientIp);

    public static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";

    /// <summary>SSH sunucusunun IP adresi (panel IP ister; host adı verildiyse çözülür).</summary>
    public static string HostAddress =>
        IPAddress.TryParse(E2EEnvironment.Host, out var ip)
            ? ip.ToString()
            : Dns.GetHostAddresses(E2EEnvironment.Host!).First(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToString();

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>Ortam yoksa testi atlar; varsa uygulamayı ve sunucu kaydını (ilk çağrıda) hazırlar.</summary>
    public async Task RequireAsync()
    {
        Assert.SkipUnless(E2EEnvironment.IsConfigured, E2EEnvironment.SkipReason);

        await _gate.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            if (_initialized)
                return;

            Factory = new E2EAppFactory();
            _ = Factory.Services; // Host'u başlatır: migration + admin seed.

            using (var scope = Factory.Services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var admin = await users.FindByEmailAsync(E2EAppFactory.AdminEmail);
                Assert.NotNull(admin);
                AdminUserId = admin.Id.ToString();
            }

            Shell = new TestShell(E2EEnvironment.Host!, E2EEnvironment.Port, E2EEnvironment.User!, E2EEnvironment.Password!);

            ServerName = Unique("e2e-server");
            ServerId = await CreateServerAsync(ServerName, E2EEnvironment.User!, useSudo: true, sudoPassword: E2EEnvironment.Password);
            var test = await AsAdminAsync(sp => sp.GetRequiredService<IServerService>().TestConnectionAsync(ServerId, TestContext.Current.CancellationToken));
            Assert.True(test.IsSuccess, test.Message);
            Assert.True(test.Data!.IsSuccess, $"E2E sunucusuna bağlanılamadı: {test.Data.Message}");

            _initialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Panelde sunucu kaydı oluşturur (bağlantı testi yapmaz).</summary>
    public async Task<Guid> CreateServerAsync(string name, string user, bool useSudo, string? sudoPassword)
    {
        var created = await AsAdminAsync(sp => sp.GetRequiredService<IServerService>().CreateAsync(new CreateServerDto
        {
            Name = name,
            Hostname = "sm-e2e-ubuntu",
            IpAddress = HostAddress,
            SshPort = E2EEnvironment.Port,
            Username = user,
            AuthenticationType = AuthenticationType.Password,
            Password = E2EEnvironment.Password,
            UseSudo = useSudo,
            SudoPassword = sudoPassword,
            MonitoringEnabled = false,
            Description = "Uçtan uca test sunucusu (otomatik oluşturuldu)"
        }, TestContext.Current.CancellationToken));
        Assert.True(created.IsSuccess, Describe(created));
        return created.Data;
    }

    /// <summary>Yeni DI kapsamında, yönetici kullanıcı adına (audit ve "oluşturan" alanları için) çalışır.</summary>
    public async Task<T> AsAdminAsync<T>(Func<IServiceProvider, Task<T>> work)
    {
        using var user = CurrentUserService.RunAs(AdminUserId, E2EAppFactory.AdminEmail, ClientIp);
        await using var scope = Factory.Services.CreateAsyncScope();
        return await work(scope.ServiceProvider);
    }

    public async Task AsAdminAsync(Func<IServiceProvider, Task> work)
    {
        using var user = CurrentUserService.RunAs(AdminUserId, E2EAppFactory.AdminEmail, ClientIp);
        await using var scope = Factory.Services.CreateAsyncScope();
        await work(scope.ServiceProvider);
    }

    public static string Describe(Application.Common.ServiceResult result) =>
        $"{result.ErrorType}: {result.Message} {string.Join("; ", result.Errors.Select(e => $"{e.PropertyName}={e.Message}"))}";

    public async ValueTask DisposeAsync()
    {
        if (_initialized)
        {
            try
            {
                await AsAdminAsync(sp => sp.GetRequiredService<IServerService>().DeleteAsync(ServerId, ServerName, CancellationToken.None));
            }
            catch (Exception)
            {
                // Veritabanı zaten kapanışta siliniyor.
            }
        }

        Shell?.Dispose();
        if (Factory is not null)
            await Factory.DisposeAsync();
        _gate.Dispose();
    }
}

[CollectionDefinition(Name)]
public sealed class E2ECollection : ICollectionFixture<E2EFixture>
{
    public const string Name = "E2E";
}
