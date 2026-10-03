using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ServerManager.Domain.Entities;
using ServerManager.Infrastructure.Identity;

namespace ServerManager.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
