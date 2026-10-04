using Microsoft.Extensions.Options;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.Plugins;
using ServerManager.Plugin.DevOps.Dokku.Core;
using ServerManager.Plugin.DevOps.Dokku.DTOs;
using ServerManager.Plugin.DevOps.Dokku.Installation;
using ServerManager.Plugin.DevOps.Dokku.Integration;

namespace ServerManager.Plugin.DevOps.Dokku.Services;

public sealed class DokkuService : IDokkuService
{
    private readonly IServerConnectionProvider _connections;
    private readonly IDokkuProvider _provider;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IPluginCatalog _catalog;
    private readonly DokkuInstallationManager _installations;
    private readonly DokkuOptions _options;

    public DokkuService(
        IServerConnectionProvider connections,
        IDokkuProvider provider,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IPluginCatalog catalog,
        DokkuInstallationManager installations,
        IOptions<DokkuOptions> options)
    {
        _connections = connections;
        _provider = provider;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _catalog = catalog;
        _installations = installations;
        _options = options.Value;
    }

    public async Task<ServiceResult<DokkuOverviewDto>> GetOverviewAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        if (!_catalog.IsEnabled(DokkuPlugin.SystemName))
            return ServiceResult<DokkuOverviewDto>.Failure("Dokku eklentisi devre dışı.");

        var connection = await _connections.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess || connection.Data is null)
        {
            return connection.ErrorType == ServiceErrorType.NotFound
                ? ServiceResult<DokkuOverviewDto>.NotFound(connection.Message ?? "Sunucu bulunamadı.")
                : ServiceResult<DokkuOverviewDto>.Success(WithInstall(new DokkuOverviewDto { HostError = connection.Message ?? "Sunucuya bağlanılamadı." }, serverId));
        }

        var timeout = TimeSpan.FromSeconds(Math.Clamp(_options.CommandTimeoutSeconds, 5, 180));
        var report = await _provider.GetReportAsync(connection.Data.Context, timeout, cancellationToken);
        if (!report.IsSuccess || report.Data is null)
            return ServiceResult<DokkuOverviewDto>.Success(WithInstall(new DokkuOverviewDto { HostError = report.Message ?? "Dokku durumu okunamadı." }, serverId));

        if (report.Data.IsInstalled)
        {
            var finished = _installations.Get(serverId);
            if (finished is { IsRunning: false, Succeeded: true })
                _installations.Clear(serverId);
        }

        return ServiceResult<DokkuOverviewDto>.Success(WithInstall(new DokkuOverviewDto
        {
            IsInstalled = report.Data.IsInstalled,
            Version = report.Data.Version,
            Apps = report.Data.Apps
        }, serverId));
    }

    public async Task<ServiceResult> StartInstallAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        if (!_catalog.IsEnabled(DokkuPlugin.SystemName))
            return ServiceResult.Failure("Dokku eklentisi devre dışı.");
        if (!DokkuCommands.IsVersion(_options.Version))
            return ServiceResult.Failure("Dokku:Version ayarı geçersiz. v0.38.31 biçiminde bir sürüm yazın.");

        var connection = await _connections.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess || connection.Data is null)
            return ServiceResult.Failure(connection.Message ?? "Sunucuya bağlanılamadı.");

        var timeout = TimeSpan.FromSeconds(Math.Clamp(_options.CommandTimeoutSeconds, 5, 180));
        var report = await _provider.GetReportAsync(connection.Data.Context, timeout, cancellationToken);
        if (!report.IsSuccess || report.Data is null)
            return ServiceResult.Failure(report.Message ?? "Kurulumdan önce Dokku durumu okunamadı.");
        if (report.Data.IsInstalled)
            return ServiceResult.Failure("Bu sunucuda Dokku zaten kurulu.");

        var actor = new DokkuActor(_currentUser.UserId, _currentUser.UserName, _currentUser.IpAddress);
        var started = _installations.Start(serverId, connection.Data.ServerName, _options.Version, actor);
        if (!started.IsSuccess)
            return started;

        await _auditLogService.LogAsync(new AuditEntry(
            DokkuAuditActions.InstallStart,
            AuditEntityTypes.Server,
            serverId.ToString(),
            connection.Data.ServerName,
            $"Sürüm: {_options.Version}"), cancellationToken);

        return started;
    }

    public async Task<ServiceResult> RestartAsync(Guid serverId, string app, CancellationToken cancellationToken = default)
    {
        if (!_catalog.IsEnabled(DokkuPlugin.SystemName))
            return ServiceResult.Failure("Dokku eklentisi devre dışı.");
        if (!DokkuCommands.IsAppName(app))
            return ServiceResult.Failure("Uygulama adı geçersiz.");

        var connection = await _connections.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess || connection.Data is null)
            return ServiceResult.Failure(connection.Message ?? "Sunucuya bağlanılamadı.");

        var timeout = TimeSpan.FromSeconds(Math.Clamp(_options.CommandTimeoutSeconds, 5, 180));
        var result = await _provider.RestartAsync(connection.Data.Context, app, timeout, cancellationToken);
        await _auditLogService.LogAsync(new AuditEntry(
            DokkuAuditActions.AppRestart,
            AuditEntityTypes.Server,
            serverId.ToString(),
            connection.Data.ServerName,
            app,
            result.IsSuccess), cancellationToken);

        return result;
    }

    private DokkuOverviewDto WithInstall(DokkuOverviewDto overview, Guid serverId)
    {
        var progress = _installations.Get(serverId);
        if (progress is null)
            return overview;

        return new DokkuOverviewDto
        {
            IsInstalled = overview.IsInstalled,
            Version = overview.Version,
            HostError = overview.HostError,
            Apps = overview.Apps,
            InstallRunning = progress.IsRunning,
            InstallSucceeded = progress.Succeeded,
            InstallMessage = progress.Message,
            InstallLog = string.IsNullOrWhiteSpace(progress.Log) ? null : progress.Log
        };
    }
}
