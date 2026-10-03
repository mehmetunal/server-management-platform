using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Services;

namespace ServerManager.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IServerService, ServerService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
