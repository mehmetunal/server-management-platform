using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ServerManager.E2E.Tests.Infrastructure;

/// <summary>
/// Uygulamayı üretim ortamı ayarlarıyla bellek içi test sunucusunda açar; testler HTTP yerine uygulama servislerini
/// gerçek DI kapsamında çağırır. Her koşu kendi geçici veritabanını açar ve kapanışta siler. Arka plan işçileri
/// (izleme, yedek zamanlayıcı …) kapatılır ki testin oluşturduğu kayıtlara kendiliğinden dokunmasınlar.
/// </summary>
public sealed class E2EAppFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@e2e.local";
    public const string AdminPassword = "E2E-Admin-Password-2026";

    private readonly string _databaseName = "SmE2E_" + Guid.NewGuid().ToString("N")[..16];
    private readonly string _workDirectory = Path.Combine(Path.GetTempPath(), "sm-e2e-tests", Guid.NewGuid().ToString("N"));
    private bool _hostCreated;

    public string BackupRootPath => Path.Combine(_workDirectory, "backups");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");

        builder.UseSetting("ConnectionStrings:DefaultConnection", E2EEnvironment.ConnectionStringFor(_databaseName));
        builder.UseSetting("Database:AutoCreate", "true");
        builder.UseSetting("Security:MasterKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("TwoFactor:Required", "false");
        builder.UseSetting("Monitoring:Enabled", "false");
        builder.UseSetting("Alerting:Enabled", "false");
        builder.UseSetting("Backup:Enabled", "false");
        builder.UseSetting("Backup:LocalRootPath", BackupRootPath);
        builder.UseSetting("Serilog:WriteTo:1:Args:path", Path.Combine(_workDirectory, "logs", "server-manager-.log"));
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");

        builder.ConfigureTestServices(services =>
        {
            var workers = services
                .Where(d => d.ServiceType == typeof(IHostedService)
                    && d.ImplementationType?.Assembly.GetName().Name?.StartsWith("ServerManager.", StringComparison.Ordinal) == true)
                .ToList();
            foreach (var worker in workers)
                services.Remove(worker);
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
            await E2EEnvironment.DropDatabaseAsync(_databaseName);

        try
        {
            if (Directory.Exists(_workDirectory))
                Directory.Delete(_workDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Serilog dosyayı geç bırakırsa geçici klasör kalabilir; testi etkilemez.
        }

        GC.SuppressFinalize(this);
    }
}
