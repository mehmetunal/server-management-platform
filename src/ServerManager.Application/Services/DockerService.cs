using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.Docker;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Docker;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.DTOs.Terminal;
using ServerManager.Application.Interfaces.Docker;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public class DockerService : IDockerService
{
    private const string InvalidContainerMessage = "Geçersiz container adı.";

    private readonly IServerConnectionProvider _connectionProvider;
    private readonly IDockerClient _dockerClient;
    private readonly IAuditLogService _auditLogService;
    private readonly IValidator<RenameContainerDto> _renameValidator;
    private readonly IValidator<PullImageDto> _pullValidator;
    private readonly IValidator<CreateVolumeDto> _volumeValidator;
    private readonly IValidator<CreateNetworkDto> _networkValidator;
    private readonly DockerOptions _options;
    private readonly ILogger<DockerService> _logger;

    public DockerService(
        IServerConnectionProvider connectionProvider,
        IDockerClient dockerClient,
        IAuditLogService auditLogService,
        IValidator<RenameContainerDto> renameValidator,
        IValidator<PullImageDto> pullValidator,
        IValidator<CreateVolumeDto> volumeValidator,
        IValidator<CreateNetworkDto> networkValidator,
        IOptions<DockerOptions> options,
        ILogger<DockerService> logger)
    {
        _connectionProvider = connectionProvider;
        _dockerClient = dockerClient;
        _auditLogService = auditLogService;
        _renameValidator = renameValidator;
        _pullValidator = pullValidator;
        _volumeValidator = volumeValidator;
        _networkValidator = networkValidator;
        _options = options.Value;
        _logger = logger;
    }

    public Task<ServiceResult<DockerOverviewDto>> GetOverviewAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        QueryAsync(serverId, (context, ct) => _dockerClient.GetOverviewAsync(context, ct), cancellationToken);

    public Task<ServiceResult<IReadOnlyList<DockerContainerDto>>> GetContainersAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        QueryAsync(serverId, (context, ct) => _dockerClient.GetContainersAsync(context, ct), cancellationToken);

    public Task<ServiceResult<IReadOnlyList<DockerContainerStatsDto>>> GetContainerStatsAsync(Guid serverId, string? container, CancellationToken cancellationToken = default)
    {
        if (container is not null && !DockerNames.IsValidContainerReference(container))
            return Task.FromResult(ServiceResult<IReadOnlyList<DockerContainerStatsDto>>.Failure(InvalidContainerMessage, ServiceErrorType.Validation));

        return QueryAsync(serverId, (context, ct) => _dockerClient.GetContainerStatsAsync(context, container, ct), cancellationToken);
    }

    public Task<ServiceResult<DockerContainerDetailsDto>> GetContainerAsync(Guid serverId, string container, CancellationToken cancellationToken = default)
    {
        if (!DockerNames.IsValidContainerReference(container))
            return Task.FromResult(ServiceResult<DockerContainerDetailsDto>.NotFound("Container bulunamadı."));

        return QueryAsync(serverId, (context, ct) => _dockerClient.GetContainerAsync(context, container, ct), cancellationToken);
    }

    public Task<ServiceResult<DockerLogsDto>> GetContainerLogsAsync(Guid serverId, DockerLogQuery query, CancellationToken cancellationToken = default)
    {
        if (!DockerNames.IsValidContainerReference(query.Container))
            return Task.FromResult(ServiceResult<DockerLogsDto>.Failure(InvalidContainerMessage, ServiceErrorType.Validation));

        string? since = null;
        if (!string.IsNullOrWhiteSpace(query.Since))
        {
            if (!DockerNames.IsValidLogSince(query.Since))
                return Task.FromResult(ServiceResult<DockerLogsDto>.Failure("Geçersiz zaman damgası.", ServiceErrorType.Validation));

            since = query.Since;
        }

        var maxTail = Math.Max(1, _options.MaxLogTail);
        var tail = Math.Clamp(query.Tail ?? _options.DefaultLogTail, 1, maxTail);
        return QueryAsync(serverId, (context, ct) => _dockerClient.GetContainerLogsAsync(context, query.Container, tail, since, ct), cancellationToken);
    }

    public async Task<ServiceResult> ExecuteContainerActionAsync(Guid serverId, ContainerActionRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.Action))
            return ServiceResult.Failure("Geçersiz işlem.", ServiceErrorType.Validation);

        if (!DockerNames.IsValidContainerReference(request.Container))
            return ServiceResult.Failure(InvalidContainerMessage, ServiceErrorType.Validation);

        if (request.Action == DockerContainerAction.Remove
            && !string.Equals(request.ConfirmationName?.Trim(), request.Container, StringComparison.Ordinal))
            return ServiceResult.ValidationFailure(nameof(request.ConfirmationName), "Onay için container adını birebir yazın.");

        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess)
            return connection;

        var force = request.Action == DockerContainerAction.Remove && request.Force;
        var result = await _dockerClient.ExecuteContainerActionAsync(connection.Data!.Context, request.Container, request.Action, force, cancellationToken);

        var details = $"Container: {request.Container}" + (force ? " (zorla)" : string.Empty);
        await AuditAsync(DockerActionPolicies.AuditAction(request.Action), connection.Data, details, result, cancellationToken);

        return result.IsSuccess
            ? ServiceResult.Success($"{request.Container} {DockerActionPolicies.DisplayName(request.Action)}.")
            : result;
    }

    public async Task<ServiceResult> RenameContainerAsync(Guid serverId, RenameContainerDto dto, CancellationToken cancellationToken = default)
    {
        dto.NewName = dto.NewName?.Trim() ?? string.Empty;
        var validation = await _renameValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        return await MutateAsync(
            serverId,
            AuditActions.DockerContainerRename,
            $"Container: {dto.Container} -> {dto.NewName}",
            $"Container yeniden adlandırıldı: {dto.NewName}.",
            (context, ct) => _dockerClient.RenameContainerAsync(context, dto.Container, dto.NewName, ct),
            cancellationToken);
    }

    public Task<ServiceResult<IReadOnlyList<DockerImageDto>>> GetImagesAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        QueryAsync(serverId, (context, ct) => _dockerClient.GetImagesAsync(context, ct), cancellationToken);

    public async Task<ServiceResult> PullImageAsync(Guid serverId, PullImageDto dto, CancellationToken cancellationToken = default)
    {
        dto.Reference = dto.Reference?.Trim() ?? string.Empty;
        var validation = await _pullValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        return await MutateAsync(
            serverId,
            AuditActions.DockerImagePull,
            $"Image: {dto.Reference}",
            $"Image indirildi: {dto.Reference}.",
            (context, ct) => _dockerClient.PullImageAsync(context, dto.Reference, ct),
            cancellationToken);
    }

    public Task<ServiceResult> RemoveImageAsync(Guid serverId, string reference, bool force, CancellationToken cancellationToken = default)
    {
        if (!DockerNames.IsValidImageReference(reference))
            return Task.FromResult(ServiceResult.Failure("Geçersiz image.", ServiceErrorType.Validation));

        return MutateAsync(
            serverId,
            AuditActions.DockerImageRemove,
            $"Image: {reference}" + (force ? " (zorla)" : string.Empty),
            $"Image silindi: {reference}.",
            (context, ct) => _dockerClient.RemoveImageAsync(context, reference, force, ct),
            cancellationToken);
    }

    public async Task<ServiceResult> PruneImagesAsync(Guid serverId, bool all, string? confirmationName, CancellationToken cancellationToken = default)
    {
        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess)
            return connection;

        if (!string.Equals(confirmationName?.Trim(), connection.Data!.ServerName, StringComparison.Ordinal))
            return ServiceResult.ValidationFailure("ConfirmationName", "Onay için sunucu adını birebir yazın.");

        var result = await _dockerClient.PruneImagesAsync(connection.Data.Context, all, cancellationToken);
        await AuditAsync(AuditActions.DockerImagePrune, connection.Data, all ? "Kullanılmayan tüm image'lar" : "Etiketsiz (dangling) image'lar", result, cancellationToken);
        return result;
    }

    public Task<ServiceResult<IReadOnlyList<DockerVolumeDto>>> GetVolumesAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        QueryAsync(serverId, (context, ct) => _dockerClient.GetVolumesAsync(context, ct), cancellationToken);

    public async Task<ServiceResult> CreateVolumeAsync(Guid serverId, CreateVolumeDto dto, CancellationToken cancellationToken = default)
    {
        dto.Name = dto.Name?.Trim() ?? string.Empty;
        var validation = await _volumeValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        return await MutateAsync(
            serverId,
            AuditActions.DockerVolumeCreate,
            $"Volume: {dto.Name}",
            $"Volume oluşturuldu: {dto.Name}.",
            (context, ct) => _dockerClient.CreateVolumeAsync(context, dto.Name, ct),
            cancellationToken);
    }

    public Task<ServiceResult> RemoveVolumeAsync(Guid serverId, string name, string? confirmationName, CancellationToken cancellationToken = default)
    {
        if (!DockerNames.IsValidVolumeReference(name))
            return Task.FromResult(ServiceResult.Failure("Geçersiz volume.", ServiceErrorType.Validation));

        if (!string.Equals(confirmationName?.Trim(), name, StringComparison.Ordinal))
            return Task.FromResult(ServiceResult.ValidationFailure("ConfirmationName", "Onay için volume adını birebir yazın."));

        return MutateAsync(
            serverId,
            AuditActions.DockerVolumeRemove,
            $"Volume: {name}",
            $"Volume silindi: {name}.",
            (context, ct) => _dockerClient.RemoveVolumeAsync(context, name, ct),
            cancellationToken);
    }

    public Task<ServiceResult<IReadOnlyList<DockerNetworkDto>>> GetNetworksAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        QueryAsync(serverId, (context, ct) => _dockerClient.GetNetworksAsync(context, ct), cancellationToken);

    public async Task<ServiceResult> CreateNetworkAsync(Guid serverId, CreateNetworkDto dto, CancellationToken cancellationToken = default)
    {
        dto.Name = dto.Name?.Trim() ?? string.Empty;
        dto.Subnet = TextHelper.NullIfEmpty(dto.Subnet);
        dto.Gateway = TextHelper.NullIfEmpty(dto.Gateway);
        var validation = await _networkValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        var details = $"Network: {dto.Name}, driver: {dto.Driver}"
                      + (dto.Subnet is null ? string.Empty : $", subnet: {dto.Subnet}")
                      + (dto.Internal ? ", internal" : string.Empty);

        return await MutateAsync(
            serverId,
            AuditActions.DockerNetworkCreate,
            details,
            $"Network oluşturuldu: {dto.Name}.",
            (context, ct) => _dockerClient.CreateNetworkAsync(context, dto, ct),
            cancellationToken);
    }

    public Task<ServiceResult> RemoveNetworkAsync(Guid serverId, string network, CancellationToken cancellationToken = default)
    {
        if (!DockerNames.IsValidNetworkReference(network))
            return Task.FromResult(ServiceResult.Failure("Geçersiz network.", ServiceErrorType.Validation));

        if (network is "bridge" or "host" or "none")
            return Task.FromResult(ServiceResult.Failure("Docker'ın varsayılan network'leri silinemez."));

        return MutateAsync(
            serverId,
            AuditActions.DockerNetworkRemove,
            $"Network: {network}",
            $"Network silindi: {network}.",
            (context, ct) => _dockerClient.RemoveNetworkAsync(context, network, ct),
            cancellationToken);
    }

    public async Task<ServiceResult<TerminalHandle>> OpenTerminalAsync(
        Guid serverId,
        string container,
        int columns,
        int rows,
        ITerminalOutputSink sink,
        CancellationToken cancellationToken = default)
    {
        if (!DockerNames.IsValidContainerReference(container))
            return ServiceResult<TerminalHandle>.Failure(InvalidContainerMessage, ServiceErrorType.Validation);

        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult<TerminalHandle>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        var result = await _dockerClient.OpenContainerTerminalAsync(
            connection.Data!.Context,
            container,
            Math.Clamp(columns, 20, 500),
            Math.Clamp(rows, 5, 200),
            sink,
            cancellationToken);

        await AuditAsync(AuditActions.DockerTerminalOpen, connection.Data, $"Container: {container}", result, cancellationToken);

        if (!result.IsSuccess)
            return ServiceResult<TerminalHandle>.Failure(result.Message ?? "Terminal açılamadı.");

        return ServiceResult<TerminalHandle>.Success(new TerminalHandle
        {
            ServerId = connection.Data.ServerId,
            ServerName = connection.Data.ServerName,
            Kind = TerminalSessionKind.Container,
            Container = container,
            Session = result.Data!
        });
    }

    private async Task<ServiceResult<T>> QueryAsync<T>(
        Guid serverId,
        Func<RemoteExecutionContext, CancellationToken, Task<ServiceResult<T>>> query,
        CancellationToken cancellationToken)
    {
        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult<T>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        return await query(connection.Data!.Context, cancellationToken);
    }

    private async Task<ServiceResult> MutateAsync(
        Guid serverId,
        string auditAction,
        string details,
        string successMessage,
        Func<RemoteExecutionContext, CancellationToken, Task<ServiceResult>> operation,
        CancellationToken cancellationToken)
    {
        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess)
            return connection;

        var result = await operation(connection.Data!.Context, cancellationToken);
        await AuditAsync(auditAction, connection.Data, details, result, cancellationToken);
        return result.IsSuccess ? ServiceResult.Success(successMessage) : result;
    }

    private async Task AuditAsync(string action, ServerConnection connection, string details, ServiceResult result, CancellationToken cancellationToken)
    {
        if (!result.IsSuccess)
        {
            _logger.LogWarning("Docker işlemi başarısız. Action: {Action}, ServerId: {ServerId}, Error: {Error}",
                action, connection.ServerId, result.Message);
        }

        await _auditLogService.LogAsync(new AuditEntry(
            action,
            AuditEntityTypes.Server,
            connection.ServerId.ToString(),
            connection.ServerName,
            result.IsSuccess ? details : $"{details} | Hata: {result.Message}",
            result.IsSuccess), cancellationToken);
    }
}
