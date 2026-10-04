using System.Globalization;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.ServerSystem;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.ServerSystem;

namespace ServerManager.Application.Services;

public sealed class ServerSystemService : IServerSystemService
{
    private readonly IServerConnectionProvider _connectionProvider;
    private readonly IServerSystemInspector _inspector;
    private readonly IAuditLogService _auditLogService;

    public ServerSystemService(IServerConnectionProvider connectionProvider, IServerSystemInspector inspector, IAuditLogService auditLogService)
    {
        _connectionProvider = connectionProvider;
        _inspector = inspector;
        _auditLogService = auditLogService;
    }

    public Task<ServiceResult<ServiceList>> GetServicesAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(serverId, (c, ct) => _inspector.GetServicesAsync(c.Context, ct), cancellationToken);

    public Task<ServiceResult<ProcessList>> GetProcessesAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(serverId, (c, ct) => _inspector.GetProcessesAsync(c.Context, ct), cancellationToken);

    public Task<ServiceResult<NetworkSnapshot>> GetNetworkAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(serverId, (c, ct) => _inspector.GetNetworkAsync(c.Context, ct), cancellationToken);

    public Task<ServiceResult<StorageSnapshot>> GetStorageAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(serverId, (c, ct) => _inspector.GetStorageAsync(c.Context, ct), cancellationToken);

    public Task<ServiceResult<LogSnapshot>> GetLogsAsync(Guid serverId, LogRequest request, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(request.Unit) && !ServerSystemRules.IsValidServiceName(request.Unit))
            return Task.FromResult(ServiceResult<LogSnapshot>.Failure("Geçersiz servis adı."));
        if (request.Source == LogSource.File && !string.IsNullOrEmpty(request.Path) && !ServerSystemRules.IsValidLogPath(request.Path))
            return Task.FromResult(ServiceResult<LogSnapshot>.Failure("Yalnızca /var/log altındaki dosyalar okunabilir."));
        if (!ServerSystemRules.IsValidPriority(request.Priority))
            return Task.FromResult(ServiceResult<LogSnapshot>.Failure("Geçersiz öncelik."));

        return WithConnectionAsync(serverId, (c, ct) => _inspector.GetLogsAsync(c.Context, request, ct), cancellationToken);
    }

    public async Task<ServiceResult<string>> ControlServiceAsync(
        Guid serverId, ServiceManagerKind manager, string name, ServiceAction action, CancellationToken cancellationToken = default)
    {
        if (manager == ServiceManagerKind.None || !Enum.IsDefined(manager) || !Enum.IsDefined(action))
            return ServiceResult<string>.Failure("Geçersiz işlem.");
        if (!ServerSystemRules.IsValidServiceName(name))
            return ServiceResult<string>.Failure("Geçersiz servis adı.");

        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess || connection.Data is null)
            return ServiceResult<string>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        var result = await _inspector.ControlServiceAsync(connection.Data.Context, manager, name, action, cancellationToken);
        var verb = ActionText(action);
        await _auditLogService.LogAsync(new AuditEntry(
            AuditActions.SystemServiceControl,
            AuditEntityTypes.Server,
            serverId.ToString(),
            connection.Data.ServerName,
            $"{name}: {verb}" + (result.IsSuccess ? string.Empty : $" — {result.Message}"),
            result.IsSuccess), cancellationToken);

        return result.IsSuccess
            ? ServiceResult<string>.Success(result.Data ?? string.Empty, $"{name} servisi için {verb} komutu çalıştırıldı.")
            : ServiceResult<string>.Failure(result.Message ?? $"{name} servisi için {verb} başarısız.");
    }

    public async Task<ServiceResult<string>> SignalProcessAsync(
        Guid serverId, int pid, ProcessSignal signal, string? processName, CancellationToken cancellationToken = default)
    {
        if (pid <= 1 || !Enum.IsDefined(signal))
            return ServiceResult<string>.Failure("Bu process sonlandırılamaz.");

        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess || connection.Data is null)
            return ServiceResult<string>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        var result = await _inspector.SignalProcessAsync(connection.Data.Context, pid, signal, cancellationToken);
        var label = string.IsNullOrWhiteSpace(processName) ? $"PID {pid.ToString(CultureInfo.InvariantCulture)}" : $"{TextHelper.Truncate(processName, 120)} (PID {pid.ToString(CultureInfo.InvariantCulture)})";
        var signalText = signal == ProcessSignal.Kill ? "SIGKILL" : "SIGTERM";
        await _auditLogService.LogAsync(new AuditEntry(
            AuditActions.SystemProcessSignal,
            AuditEntityTypes.Server,
            serverId.ToString(),
            connection.Data.ServerName,
            $"{label}: {signalText}" + (result.IsSuccess ? string.Empty : $" — {result.Message}"),
            result.IsSuccess), cancellationToken);

        return result.IsSuccess
            ? ServiceResult<string>.Success(result.Data ?? string.Empty, $"{label} için {signalText} gönderildi.")
            : ServiceResult<string>.Failure(result.Message ?? "Process sonlandırılamadı.");
    }

    private static string ActionText(ServiceAction action) => action switch
    {
        ServiceAction.Start => "başlatma",
        ServiceAction.Stop => "durdurma",
        _ => "yeniden başlatma"
    };

    private async Task<ServiceResult<T>> WithConnectionAsync<T>(
        Guid serverId, Func<ServerConnection, CancellationToken, Task<ServiceResult<T>>> work, CancellationToken cancellationToken)
    {
        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess || connection.Data is null)
            return ServiceResult<T>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        return await work(connection.Data, cancellationToken);
    }
}
