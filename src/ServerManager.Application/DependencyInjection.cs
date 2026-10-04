using ServerManager.Application.Cloud;
using ServerManager.Application.Interfaces.Cloud;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ServerManager.Application.Auditing;
using ServerManager.Application.Backups;
using ServerManager.Application.Interfaces.Backups;
using ServerManager.Application.Deployments;
using ServerManager.Application.Interfaces.Deployments;
using ServerManager.Application.Interfaces.Notifications;
using ServerManager.Application.Notifications;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.Services;
using ServerManager.Application.Terminal;

namespace ServerManager.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IServerService, ServerService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IMonitoringService, MonitoringService>();
        services.AddScoped<IServerConnectionProvider, ServerConnectionProvider>();
        services.AddScoped<IDockerService, DockerService>();
        services.AddScoped<ITerminalService, TerminalService>();
        services.AddScoped<IFileService, FileService>();
        services.AddScoped<IPluginService, PluginService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IDeploymentService, DeploymentService>();
        services.AddScoped<IGitIntegrationRegistry, GitIntegrationRegistry>();
        services.AddScoped<INotificationChannelRegistry, NotificationChannelRegistry>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<INotificationChannelService, NotificationChannelService>();
        services.AddScoped<IAlertRuleService, AlertRuleService>();
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IUptimeService, UptimeService>();
        services.AddScoped<ISslCertificateService, SslCertificateService>();
        services.AddScoped<IBackupStorageRegistry, BackupStorageRegistry>();
        services.AddScoped<IBackupStorageService, BackupStorageService>();
        services.AddScoped<IServerGroupService, ServerGroupService>();
        services.AddScoped<IServerTemplateService, ServerTemplateService>();
        services.AddScoped<ICommandRunService, CommandRunService>();
        services.AddScoped<ICloudProviderRegistry, CloudProviderRegistry>();
        services.AddScoped<ICloudAccountService, CloudAccountService>();
        services.AddScoped<ICostReportService, CostReportService>();
        services.AddScoped<IBackupJobService, BackupJobService>();
        services.AddScoped<IBackupRunService, BackupRunService>();
        services.AddScoped<ISecurityService, SecurityService>();
        services.AddScoped<IServerSystemService, ServerSystemService>();
        services.AddSingleton<AuditActionCatalog>();
        services.AddSingleton<DangerousCommandDetector>();
        services.AddSingleton<TerminalCommandQueue>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
