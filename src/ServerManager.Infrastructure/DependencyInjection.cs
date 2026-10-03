using FluentMigrator.Runner;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Authorization;
using ServerManager.Application.Deployments;
using ServerManager.Application.Docker;
using ServerManager.Application.Files;
using ServerManager.Application.Plugins;
using ServerManager.Application.Terminal;
using ServerManager.Application.Interfaces.Deployments;
using ServerManager.Application.Interfaces.Docker;
using ServerManager.Application.Interfaces.Files;
using ServerManager.Application.Interfaces.Monitoring;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Monitoring;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.Validators.Users;
using ServerManager.Infrastructure.Deployments;
using ServerManager.Infrastructure.Docker;
using ServerManager.Infrastructure.Files;
using ServerManager.Infrastructure.Identity;
using ServerManager.Infrastructure.Monitoring;
using ServerManager.Infrastructure.Persistence;
using ServerManager.Infrastructure.Plugins;
using ServerManager.Infrastructure.Repositories;
using ServerManager.Infrastructure.Security;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = GetConnectionString(configuration);

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3)));

        services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
            {
                options.Password.RequiredLength = UserPasswordRules.MinimumLength;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.User.RequireUniqueEmail = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddErrorDescriber<TurkishIdentityErrorDescriber>()
            .AddDefaultTokenProviders();

        services.AddFluentMigratorCore()
            .ConfigureRunner(rb => rb
                .AddSqlServer()
                .WithGlobalConnectionString(connectionString)
                .ScanIn(typeof(DependencyInjection).Assembly).For.Migrations())
            .AddLogging(lb => lb.AddFluentMigratorConsole());

        services.AddOptions<SecurityOptions>()
            .Bind(configuration.GetSection(SecurityOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SecurityOptions>, SecurityOptionsValidator>();
        services.Configure<SshOptions>(configuration.GetSection(SshOptions.SectionName));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.Configure<MonitoringOptions>(configuration.GetSection(MonitoringOptions.SectionName));
        services.Configure<DockerOptions>(configuration.GetSection(DockerOptions.SectionName));
        services.Configure<TerminalOptions>(configuration.GetSection(TerminalOptions.SectionName));
        services.Configure<FileManagerOptions>(configuration.GetSection(FileManagerOptions.SectionName));
        services.Configure<PluginOptions>(configuration.GetSection(PluginOptions.SectionName));
        services.Configure<DeploymentOptions>(configuration.GetSection(DeploymentOptions.SectionName));

        services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
        services.AddSingleton<ISshConnectionTester, SshNetConnectionTester>();
        services.AddSingleton<IMetricsCollector, SshMetricsCollector>();
        services.AddSingleton<IRemoteCommandRunner, SshRemoteCommandRunner>();
        services.AddSingleton<ITerminalSessionFactory, SshTerminalSessionFactory>();
        services.AddSingleton<IDockerClient, SshDockerClient>();
        services.AddSingleton<IRemoteFileSystem, SftpRemoteFileSystem>();
        services.AddSingleton<IDeploymentProvider, SshDeploymentProvider>();
        services.AddSingleton<IPluginMigrator>(provider =>
            new FluentPluginMigrator(connectionString, provider.GetRequiredService<ILoggerFactory>()));

        services.AddScoped<IServerRepository, ServerRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IServerMetricRepository, ServerMetricRepository>();
        services.AddScoped<ITerminalLogRepository, TerminalLogRepository>();
        services.AddScoped<IPluginRepository, PluginRepository>();
        services.AddScoped<IDeploymentRepository, DeploymentRepository>();

        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IdentitySeeder>();
        services.AddScoped<IPermissionSeeder>(provider => provider.GetRequiredService<IdentitySeeder>());

        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DatabaseInitialization");

        if (configuration.GetValue("Database:AutoCreate", false))
            await DatabaseBootstrapper.EnsureDatabaseExistsAsync(GetConnectionString(configuration), logger);

        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
        try
        {
            runner.MigrateUp();
            logger.LogInformation("FluentMigrator MigrateUp tamamlandı.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "FluentMigrator hatası");
            throw;
        }

        var seeder = scope.ServiceProvider.GetRequiredService<IdentitySeeder>();
        await seeder.SeedAsync();

        await scope.ServiceProvider.GetRequiredService<IPluginService>().InitializeAsync();
    }

    private static string GetConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection tanımlı değil. user-secrets veya ortam değişkeni (ConnectionStrings__DefaultConnection) ile verin.");

        return connectionString;
    }
}
