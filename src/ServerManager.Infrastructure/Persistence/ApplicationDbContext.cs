using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ServerManager.Application.Plugins;
using ServerManager.Domain.Entities;
using ServerManager.Infrastructure.Identity;

namespace ServerManager.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private readonly IPluginCatalog? _pluginCatalog;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IPluginCatalog? pluginCatalog = null) : base(options)
    {
        _pluginCatalog = pluginCatalog;
    }

    public DbSet<Server> Servers => Set<Server>();

    public DbSet<ServerCredential> ServerCredentials => Set<ServerCredential>();

    public DbSet<ServerTag> ServerTags => Set<ServerTag>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<ServerMetric> ServerMetrics => Set<ServerMetric>();

    public DbSet<ServerMetricHourly> ServerMetricsHourly => Set<ServerMetricHourly>();

    public DbSet<ServerHealthCheck> ServerHealthChecks => Set<ServerHealthCheck>();

    public DbSet<ServerMetricSnapshot> ServerMetricSnapshots => Set<ServerMetricSnapshot>();

    public DbSet<TerminalSessionLog> TerminalSessions => Set<TerminalSessionLog>();

    public DbSet<TerminalCommandLog> TerminalCommands => Set<TerminalCommandLog>();

    public DbSet<InstalledPlugin> InstalledPlugins => Set<InstalledPlugin>();

    public DbSet<DeploymentProject> DeploymentProjects => Set<DeploymentProject>();

    public DbSet<Deployment> Deployments => Set<Deployment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Model bir kez kurulup önbelleğe alınır; eklenti listesi açılışta sabitlendiği için bu güvenlidir.
        foreach (var assembly in _pluginCatalog?.LoadedAssemblies ?? [])
            builder.ApplyConfigurationsFromAssembly(assembly);
    }
}
