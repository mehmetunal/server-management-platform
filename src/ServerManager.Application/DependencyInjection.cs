using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ServerManager.Application.Auditing;
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
        services.AddSingleton<AuditActionCatalog>();
        services.AddSingleton<DangerousCommandDetector>();
        services.AddSingleton<TerminalCommandQueue>();
        services.TryAddSingleton(TimeProvider.System);

        return services;
    }
}
