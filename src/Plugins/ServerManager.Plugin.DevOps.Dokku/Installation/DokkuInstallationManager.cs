using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.Plugins;
using ServerManager.Plugin.DevOps.Dokku.Core;
using ServerManager.Plugin.DevOps.Dokku.DTOs;
using ServerManager.Plugin.DevOps.Dokku.Services;

namespace ServerManager.Plugin.DevOps.Dokku.Installation;

public sealed class DokkuInstallationManager
{
    private readonly ConcurrentDictionary<Guid, DokkuInstallRun> _runs = new();
    private readonly IServiceScopeFactory _scopes;
    private readonly IPluginCatalog _catalog;
    private readonly ILogger<DokkuInstallationManager> _logger;

    public DokkuInstallationManager(IServiceScopeFactory scopes, IPluginCatalog catalog, ILogger<DokkuInstallationManager> logger)
    {
        _scopes = scopes;
        _catalog = catalog;
        _logger = logger;
    }

    public DokkuInstallProgress? Get(Guid serverId) =>
        _runs.TryGetValue(serverId, out var run) ? run.Snapshot() : null;

    public void Clear(Guid serverId) => _runs.TryRemove(serverId, out _);

    public ServiceResult Start(Guid serverId, string serverName, string version, DokkuActor actor)
    {
        var run = new DokkuInstallRun();
        while (true)
        {
            if (_runs.TryAdd(serverId, run))
                break;

            if (!_runs.TryGetValue(serverId, out var current))
                continue;

            if (current.Snapshot().IsRunning || !_runs.TryUpdate(serverId, run, current))
                return ServiceResult.Failure("Bu sunucuda Dokku kurulumu zaten sürüyor.");

            break;
        }

        _ = Task.Run(() => ExecuteAsync(serverId, serverName, version, actor, run));
        return ServiceResult.Success("Dokku kurulumu başlatıldı. Çıktı bu sayfada yenilenir.");
    }

    private async Task ExecuteAsync(Guid serverId, string serverName, string version, DokkuActor actor, DokkuInstallRun run)
    {
        var message = "Dokku kurulumu başarısız.";
        var succeeded = false;
        try
        {
            if (!_catalog.IsEnabled(DokkuPlugin.SystemName))
            {
                message = "Dokku eklentisi devre dışı.";
                return;
            }

            await using var scope = _scopes.CreateAsyncScope();
            var connections = scope.ServiceProvider.GetRequiredService<IServerConnectionProvider>();
            var provider = scope.ServiceProvider.GetRequiredService<IDokkuProvider>();
            var options = scope.ServiceProvider.GetRequiredService<IOptions<DokkuOptions>>().Value;
            var connection = await connections.GetAsync(serverId, CancellationToken.None);
            if (!connection.IsSuccess || connection.Data is null)
            {
                message = connection.Message ?? "Sunucuya bağlanılamadı.";
                return;
            }

            var timeout = TimeSpan.FromMinutes(Math.Clamp(options.InstallTimeoutMinutes, 5, 60));
            var result = await provider.InstallAsync(
                connection.Data.Context,
                version,
                timeout,
                (line, _) =>
                {
                    run.Append(line);
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            succeeded = result.IsSuccess;
            message = result.Message ?? (succeeded ? "Dokku kuruldu." : "Dokku kurulumu başarısız.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dokku kurulumu durdu. ServerId: {ServerId}", serverId);
            message = "Dokku kurulumu beklenmeyen bir hatayla durdu.";
        }
        finally
        {
            run.Complete(succeeded, message);
            await LogCompletionAsync(serverId, serverName, actor, succeeded, message);
        }
    }

    private async Task LogCompletionAsync(Guid serverId, string serverName, DokkuActor actor, bool succeeded, string message)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
            await audit.LogAsync(new AuditEntry(
                DokkuAuditActions.InstallComplete,
                AuditEntityTypes.Server,
                serverId.ToString(),
                serverName,
                message,
                succeeded,
                UserNameOverride: actor.UserName,
                UserIdOverride: actor.UserId,
                IpAddressOverride: actor.IpAddress));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dokku kurulum sonucu audit kaydına yazılamadı. ServerId: {ServerId}", serverId);
        }
    }
}
