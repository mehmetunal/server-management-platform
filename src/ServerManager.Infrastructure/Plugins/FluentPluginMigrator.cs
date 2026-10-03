using System.Reflection;
using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServerManager.Application.Plugins;

namespace ServerManager.Infrastructure.Plugins;

/// <summary>
/// Her eklenti için yalnızca o eklentinin assembly'sini tarayan ayrı bir runner kurar. Sürümler çekirdekle aynı
/// VersionInfo tablosunda tutulduğu için eklenti migration numaraları tarih-saat biçiminde benzersiz olmalıdır.
/// </summary>
public sealed class FluentPluginMigrator : IPluginMigrator
{
    private readonly string _connectionString;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<FluentPluginMigrator> _logger;

    public FluentPluginMigrator(string connectionString, ILoggerFactory loggerFactory)
    {
        _connectionString = connectionString;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<FluentPluginMigrator>();
    }

    public void MigrateUp(Assembly assembly)
    {
        // FluentMigrator migration bulamayınca hata fırlatır; tablo gerektirmeyen eklentiler (bildirim kanalları) atlanır.
        if (!HasMigrations(assembly))
            return;

        using var provider = new ServiceCollection()
            .AddSingleton(_loggerFactory)
            .AddLogging()
            .AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddSqlServer()
                .WithGlobalConnectionString(_connectionString)
                .ScanIn(assembly).For.Migrations())
            .BuildServiceProvider(validateScopes: false);

        using var scope = provider.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        if (!runner.HasMigrationsToApplyUp())
            return;

        runner.MigrateUp();
        _logger.LogInformation("Eklenti migration'ları uygulandı: {Assembly}", assembly.GetName().Name);
    }

    public static bool HasMigrations(Assembly assembly)
    {
        Type?[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types;
        }

        return types.Any(t => t is { IsAbstract: false } && typeof(FluentMigrator.IMigration).IsAssignableFrom(t)
                              && t.GetCustomAttribute<FluentMigrator.MigrationAttribute>() is not null);
    }
}
