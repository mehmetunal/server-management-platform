using System.Globalization;
using System.Security.Cryptography;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.Docker;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Docker;
using ServerManager.Application.DTOs.ManagedServices;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.DTOs.Terminal;
using ServerManager.Application.Interfaces.Docker;
using ServerManager.Application.Interfaces.ManagedServices;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.ManagedServices;
using ServerManager.Application.Validators.ManagedServices;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public class ManagedServiceService : IManagedServiceService
{
    public const string InterruptedReason = "Uygulama kapandığı için işlem yarıda kesildi; servisin durumunu kontrol edip işlemi yeniden başlatın.";
    private const string BusyMessage = "Bu servis için süren bir işlem var; bitmesini bekleyin.";
    private const string NotFoundMessage = "Servis bulunamadı.";
    private const string DecryptFailedMessage = "Servisin kimlik bilgileri çözülemedi. Master key değişmiş olabilir.";
    private const int OperationHistorySize = 50;

    private readonly IManagedServiceRepository _repository;
    private readonly IServerRepository _serverRepository;
    private readonly IServerConnectionProvider _connectionProvider;
    private readonly IManagedServiceProvider _provider;
    private readonly IDockerClient _dockerClient;
    private readonly ISecretProtector _secretProtector;
    private readonly IAuditLogService _auditLogService;
    private readonly IValidator<CreateManagedServiceDto> _createValidator;
    private readonly IValidator<UpdateManagedServiceDto> _updateValidator;
    private readonly IValidator<UpgradeManagedServiceDto> _upgradeValidator;
    private readonly ManagedServiceOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ManagedServiceService> _logger;
    private readonly IServiceTemplateCatalog _templates;

    public ManagedServiceService(
        IManagedServiceRepository repository,
        IServerRepository serverRepository,
        IServerConnectionProvider connectionProvider,
        IManagedServiceProvider provider,
        IDockerClient dockerClient,
        ISecretProtector secretProtector,
        IAuditLogService auditLogService,
        IValidator<CreateManagedServiceDto> createValidator,
        IValidator<UpdateManagedServiceDto> updateValidator,
        IValidator<UpgradeManagedServiceDto> upgradeValidator,
        IOptions<ManagedServiceOptions> options,
        TimeProvider timeProvider,
        ILogger<ManagedServiceService> logger,
        IServiceTemplateCatalog templates)
    {
        _templates = templates;
        _repository = repository;
        _serverRepository = serverRepository;
        _connectionProvider = connectionProvider;
        _provider = provider;
        _dockerClient = dockerClient;
        _secretProtector = secretProtector;
        _auditLogService = auditLogService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _upgradeValidator = upgradeValidator;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    // ---------------------------------------------------------------- Entegrasyon

    public async Task<ServiceResult<ManagedServiceConnectionInfo>> GetConnectionInfoAsync(Guid serviceId, CancellationToken cancellationToken = default)
    {
        var service = await _repository.GetAsync(serviceId, cancellationToken);
        if (service is null || service.Status == ManagedServiceStatus.Removed)
            return ServiceResult<ManagedServiceConnectionInfo>.NotFound(NotFoundMessage);

        var template = _templates.Resolve(service.TemplateKey).Template;
        if (template?.PrimaryPort is null)
            return ServiceResult<ManagedServiceConnectionInfo>.Failure("Servis şablonu bulunamadı.");

        if (!TryReadCredentials(service, out var credentials))
            return ServiceResult<ManagedServiceConnectionInfo>.Failure(DecryptFailedMessage);

        var endpoint = new ServiceEndpoint(service.ContainerName, template.PrimaryPort.ContainerPort);
        const string placeholder = "SMPASSWORDMASK";
        var masked = credentials with
        {
            Password = credentials.Password is null ? null : placeholder,
            EncryptionKey = credentials.EncryptionKey is null ? null : placeholder
        };
        var preview = template.SuggestedEnvironment(endpoint, masked)
            .ToDictionary(pair => pair.Key, pair => pair.Value.Replace(placeholder, SecretMasker.Mask, StringComparison.Ordinal), StringComparer.Ordinal);
        return ServiceResult<ManagedServiceConnectionInfo>.Success(new ManagedServiceConnectionInfo
        {
            ServiceId = service.Id,
            ServerId = service.ServerId,
            Name = service.Name,
            TemplateKey = service.TemplateKey,
            Category = template.Category,
            Host = endpoint.Host,
            Port = endpoint.Port,
            Username = credentials.Username,
            Password = credentials.Password,
            Database = credentials.Database,
            ConnectionString = template.ConnectionString(endpoint, credentials),
            SuggestedEnvironment = template.SuggestedEnvironment(endpoint, credentials),
            SuggestedEnvironmentPreview = preview,
            Networks = NetworksOf(service)
        });
    }

    // ---------------------------------------------------------------- Okuma

    public async Task<IReadOnlyList<ManagedServiceListItemDto>> ListAsync(Guid? serverId, CancellationToken cancellationToken = default)
    {
        var services = await _repository.ListAsync(serverId, cancellationToken);
        return services.Select(s => ToListItem(s)).ToList();
    }

    public async Task<ServiceResult<ManagedServiceDetailsDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await _repository.GetAsync(id, cancellationToken);
        if (service is null)
            return ServiceResult<ManagedServiceDetailsDto>.NotFound(NotFoundMessage);

        var template = _templates.Resolve(service.TemplateKey).Template;
        TryReadCredentials(service, out var credentials);
        var environment = TryReadEnvironment(service);
        var running = await _repository.GetRunningOperationAsync(id, cancellationToken);
        var item = ToListItem(service);
        // Önizlemede parola yerine URL'de kaçış gerektirmeyen bir yer tutucu kullanılır, sonra **** ile değiştirilir.
        const string placeholder = "SMPASSWORDMASK";
        var masked = credentials with { Password = credentials.Password is null ? null : placeholder };
        string? Preview(ServiceEndpoint? endpoint) =>
            template is null || endpoint is null ? null : template.ConnectionString(endpoint, masked)?.Replace(placeholder, SecretMasker.Mask, StringComparison.Ordinal);

        ServiceEndpoint? internalEndpoint = null;
        ServiceEndpoint? externalEndpoint = null;
        if (template?.PrimaryPort is { } primary)
        {
            internalEndpoint = new ServiceEndpoint(service.ContainerName, primary.ContainerPort);
            var published = item.Ports.FirstOrDefault(p => p.ContainerPort == primary.ContainerPort);
            if (published is not null)
                externalEndpoint = new ServiceEndpoint(service.ExposePublicly ? item.ServerAddress : ServicePortBindings.LoopbackAddress, published.HostPort);
        }

        ServiceValidation.TryParseCidrs(service.AllowedSourceIps, out var allowed, out _);
        return ServiceResult<ManagedServiceDetailsDto>.Success(new ManagedServiceDetailsDto
        {
            Id = item.Id,
            ServerId = item.ServerId,
            ServerName = item.ServerName,
            ServerAddress = item.ServerAddress,
            Name = item.Name,
            Slug = item.Slug,
            TemplateKey = item.TemplateKey,
            Template = item.Template,
            TemplateAvailability = item.TemplateAvailability,
            TemplateBadge = item.TemplateBadge,
            TemplateBlockedMessage = item.TemplateBlockedMessage,
            ImageTag = item.ImageTag,
            ContainerName = item.ContainerName,
            Status = item.Status,
            LastError = item.LastError,
            ExposePublicly = item.ExposePublicly,
            HasAllowList = item.HasAllowList,
            Ports = item.Ports,
            WebUiUrl = item.WebUiUrl,
            CreatedAt = item.CreatedAt,
            CreatedBy = service.CreatedBy,
            Username = credentials.Username,
            Database = credentials.Database,
            HasPassword = !string.IsNullOrEmpty(credentials.Password) || !string.IsNullOrEmpty(credentials.EncryptionKey),
            VolumeMode = service.VolumeMode,
            HostDataPath = service.HostDataPath,
            VolumeName = ManagedServiceNames.VolumeName(service.Slug),
            DataPath = template?.DataPath(service.ImageTag),
            MemoryLimitMb = service.MemoryLimitMb,
            CpuLimit = service.CpuLimit,
            JoinProxyNetwork = service.JoinProxyNetwork,
            Networks = NetworksOf(service),
            AllowedSources = allowed,
            EnvironmentKeys = ManagedServiceSettingsRules.FromEnvironmentText(environment).Select(e => e.Key!).ToList(),
            InternalEndpoint = internalEndpoint,
            ExternalEndpoint = externalEndpoint,
            InternalConnectionPreview = Preview(internalEndpoint),
            ExternalConnectionPreview = Preview(externalEndpoint),
            RunningOperationId = running?.Id
        });
    }

    public async Task<ServiceResult<UpdateManagedServiceDto>> GetSettingsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await _repository.GetAsync(id, cancellationToken);
        if (service is null)
            return ServiceResult<UpdateManagedServiceDto>.NotFound(NotFoundMessage);

        var template = _templates.Resolve(service.TemplateKey).Template;
        if (template is null)
            return ServiceResult<UpdateManagedServiceDto>.Failure("Servis şablonu bulunamadı.");

        var bindings = ServicePortBindings.FromJson(service.PortBindings);
        return ServiceResult<UpdateManagedServiceDto>.Success(new UpdateManagedServiceDto
        {
            Id = service.Id,
            Ports = template.Ports.Select(p =>
            {
                var hostPort = bindings.FirstOrDefault(b => b.ContainerPort == p.ContainerPort)?.HostPort;
                return new ServicePortFormItem { ContainerPort = p.ContainerPort, Publish = hostPort is not null, HostPort = hostPort ?? p.ContainerPort };
            }).ToList(),
            ExposePublicly = service.ExposePublicly,
            AllowedSourceIps = service.AllowedSourceIps,
            Environment = ManagedServiceSettingsRules.FromEnvironmentText(TryReadEnvironment(service)),
            MemoryLimitMb = service.MemoryLimitMb,
            CpuLimit = service.CpuLimit,
            JoinProxyNetwork = service.JoinProxyNetwork,
            Networks = service.Networks
        });
    }

    public async Task<IReadOnlyList<ManagedServiceOperationDto>> ListOperationsAsync(Guid serviceId, CancellationToken cancellationToken = default)
    {
        var operations = await _repository.ListOperationsAsync(serviceId, OperationHistorySize, cancellationToken);
        return operations.Select(o => ToOperationDto(o, includeLog: false)).ToList();
    }

    public async Task<ServiceResult<ManagedServiceOperationDto>> GetOperationAsync(Guid operationId, bool includeLog, CancellationToken cancellationToken = default)
    {
        var operation = await _repository.GetOperationAsync(operationId, cancellationToken);
        return operation is null
            ? ServiceResult<ManagedServiceOperationDto>.NotFound("İşlem kaydı bulunamadı.")
            : ServiceResult<ManagedServiceOperationDto>.Success(ToOperationDto(operation, includeLog));
    }

    public async Task<ServiceResult<ManagedServiceSecretsDto>> RevealSecretsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await _repository.GetAsync(id, cancellationToken);
        if (service is null)
            return ServiceResult<ManagedServiceSecretsDto>.NotFound(NotFoundMessage);

        var template = _templates.Resolve(service.TemplateKey).Template;
        if (!TryReadCredentials(service, out var credentials))
        {
            await AuditAsync(AuditActions.ManagedServiceRevealSecrets, service, "Kimlik bilgileri çözülemedi", false, null, cancellationToken);
            return ServiceResult<ManagedServiceSecretsDto>.Failure(DecryptFailedMessage);
        }

        var item = ToListItem(service);
        string? internalConnection = null;
        string? externalConnection = null;
        IReadOnlyDictionary<string, string> suggested = new Dictionary<string, string>();
        if (template?.PrimaryPort is { } primary)
        {
            var internalEndpoint = new ServiceEndpoint(service.ContainerName, primary.ContainerPort);
            internalConnection = template.ConnectionString(internalEndpoint, credentials);
            suggested = template.SuggestedEnvironment(internalEndpoint, credentials);
            var published = item.Ports.FirstOrDefault(p => p.ContainerPort == primary.ContainerPort);
            if (published is not null)
            {
                var host = service.ExposePublicly ? item.ServerAddress : ServicePortBindings.LoopbackAddress;
                externalConnection = template.ConnectionString(new ServiceEndpoint(host, published.HostPort), credentials);
            }
        }

        await AuditAsync(AuditActions.ManagedServiceRevealSecrets, service, "Kimlik bilgileri görüntülendi", true, null, cancellationToken);
        return ServiceResult<ManagedServiceSecretsDto>.Success(new ManagedServiceSecretsDto
        {
            Username = credentials.Username,
            Password = credentials.Password,
            Database = credentials.Database,
            EncryptionKey = credentials.EncryptionKey,
            InternalConnectionString = internalConnection,
            ExternalConnectionString = externalConnection,
            SuggestedEnvironment = suggested
        });
    }

    // ---------------------------------------------------------------- Sunucu

    public async Task<ServiceResult<ServiceHostProbe>> ProbeServerAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult<ServiceHostProbe>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        return await _provider.ProbeAsync(connection.Data!.Context, cancellationToken);
    }

    public async Task<ServiceResult<IReadOnlyList<string>>> ListServerNetworksAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult<IReadOnlyList<string>>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        return await _provider.ListNetworksAsync(connection.Data!.Context, cancellationToken);
    }

    public Task<ServiceResult<ServiceRuntimeState>> GetRuntimeAsync(Guid id, CancellationToken cancellationToken = default) =>
        WithServiceAsync<ServiceRuntimeState>(id, (service, context, ct) => _provider.GetRuntimeAsync(context, service.Slug, ct), cancellationToken);

    public Task<ServiceResult<DockerLogsDto>> GetLogsAsync(Guid id, int? tail, string? since, CancellationToken cancellationToken = default)
    {
        if (since is not null && !DockerNames.IsValidLogSince(since))
            return Task.FromResult(ServiceResult<DockerLogsDto>.Failure("Geçersiz zaman damgası.", ServiceErrorType.Validation));

        var lines = Math.Clamp(tail ?? _options.DefaultLogTail, 1, 5000);
        return WithServiceAsync<DockerLogsDto>(id, (service, context, ct) => _dockerClient.GetContainerLogsAsync(context, service.ContainerName, lines, since, ct), cancellationToken);
    }

    public async Task<ServiceResult> ExecuteContainerActionAsync(Guid id, DockerContainerAction action, CancellationToken cancellationToken = default)
    {
        if (action is not (DockerContainerAction.Start or DockerContainerAction.Stop or DockerContainerAction.Restart))
            return ServiceResult.Failure("Geçersiz işlem.", ServiceErrorType.Validation);

        var service = await _repository.GetAsync(id, cancellationToken);
        if (service is null)
            return ServiceResult.NotFound(NotFoundMessage);

        if (await _repository.GetRunningOperationAsync(id, cancellationToken) is not null)
            return ServiceResult.Failure(BusyMessage, ServiceErrorType.Conflict);

        var connection = await _connectionProvider.GetAsync(service.ServerId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        var result = await _dockerClient.ExecuteContainerActionAsync(connection.Data!.Context, service.ContainerName, action, false, cancellationToken);
        var verb = action switch
        {
            DockerContainerAction.Start => "başlatıldı",
            DockerContainerAction.Stop => "durduruldu",
            _ => "yeniden başlatıldı"
        };

        await AuditAsync(AuditActions.ManagedServiceContainerAction, service,
            result.IsSuccess ? $"Container {verb}" : $"İşlem: {action} | Hata: {result.Message}", result.IsSuccess, null, cancellationToken);
        return result.IsSuccess ? ServiceResult.Success($"{service.Name} {verb}.") : result;
    }

    public async Task<ServiceResult> ReapplyFirewallAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var service = await _repository.GetAsync(id, cancellationToken);
        if (service is null)
            return ServiceResult.NotFound(NotFoundMessage);

        var template = _templates.Resolve(service.TemplateKey).Template;
        if (template is null)
            return ServiceResult.Failure("Servis şablonu bulunamadı.");

        if (await _repository.GetRunningOperationAsync(id, cancellationToken) is not null)
            return ServiceResult.Failure(BusyMessage, ServiceErrorType.Conflict);

        var connection = await _connectionProvider.GetAsync(service.ServerId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        var plan = FirewallPlan(service, template);
        var result = await _provider.ApplyFirewallAsync(connection.Data!.Context, plan, cancellationToken);
        var details = plan.HasRules
            ? $"Portlar: {string.Join(", ", plan.Ports)} | İzinli: {string.Join(", ", plan.AllowedSources)}"
            : "Kurallar kaldırıldı (kısıtlama yok)";
        await AuditAsync(AuditActions.ManagedServiceFirewallApply, service, result.IsSuccess ? details : $"{details} | Hata: {result.Message}", result.IsSuccess, null, cancellationToken);
        return result.IsSuccess
            ? ServiceResult.Success(plan.HasRules ? "Güvenlik duvarı kuralları yeniden uygulandı." : "Kısıtlama tanımlı olmadığı için kurallar kaldırıldı.")
            : result;
    }

    public async Task<ServiceResult<TerminalHandle>> OpenConsoleAsync(
        Guid id,
        int columns,
        int rows,
        ServiceActor actor,
        ITerminalOutputSink sink,
        CancellationToken cancellationToken = default)
    {
        var service = await _repository.GetAsync(id, cancellationToken);
        if (service is null || service.Status == ManagedServiceStatus.Removed)
            return ServiceResult<TerminalHandle>.NotFound(NotFoundMessage);

        var template = _templates.Find(service.TemplateKey);
        var connection = await _connectionProvider.GetAsync(service.ServerId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult<TerminalHandle>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        // Komut yalnızca şablondan gelir; istemciden komut alınmaz.
        var result = await _provider.OpenConsoleAsync(
            connection.Data!.Context,
            service.ContainerName,
            template?.ConsoleCommand,
            Math.Clamp(columns, 20, 500),
            Math.Clamp(rows, 5, 200),
            sink,
            cancellationToken);

        var label = template?.ConsoleLabel ?? "kabuk";
        await AuditAsync(AuditActions.ManagedServiceConsoleOpen, service,
            result.IsSuccess ? $"Konsol: {label}" : $"Konsol: {label} | Hata: {result.Message}", result.IsSuccess, actor, cancellationToken);

        if (!result.IsSuccess)
            return ServiceResult<TerminalHandle>.Failure(result.Message ?? "Konsol açılamadı.", result.ErrorType);

        return ServiceResult<TerminalHandle>.Success(new TerminalHandle
        {
            ServerId = connection.Data.ServerId,
            ServerName = connection.Data.ServerName,
            Kind = TerminalSessionKind.ServiceConsole,
            Container = service.ContainerName,
            Session = result.Data!
        });
    }

    // ---------------------------------------------------------------- İşlem başlatma

    public async Task<ServiceResult<ServiceOperationStart>> BeginInstallAsync(CreateManagedServiceDto dto, ServiceActor actor, CancellationToken cancellationToken = default)
    {
        dto.Name = dto.Name?.Trim();
        dto.Username = dto.Username?.Trim();
        dto.Database = dto.Database?.Trim();
        dto.ImageTag = dto.ImageTag?.Trim();
        dto.HostDataPath = TextHelper.NullIfEmpty(dto.HostDataPath);

        var template = _templates.Find(dto.TemplateKey);
        if (template is not null)
        {
            if (template.Credentials.UsernameKind == ServiceUsernameKind.Fixed)
                dto.Username = template.Credentials.DefaultUsername;
            if (template.Credentials.HasPassword && string.IsNullOrEmpty(dto.Password))
                dto.Password = ServiceSecrets.GeneratePassword();
            if (string.IsNullOrEmpty(dto.ImageTag))
                dto.ImageTag = template.DefaultTag;
        }

        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<ServiceOperationStart>.ValidationFailure(validation);

        template = _templates.Find(dto.TemplateKey)!;
        var hookErrors = ValidateWithHooks(template, new ServiceTemplateFormContext
        {
            TemplateKey = template.Key,
            IsInstall = true,
            Name = dto.Name,
            ImageTag = dto.ImageTag,
            Username = dto.Username,
            Database = dto.Database,
            Environment = EnvironmentDictionary(dto.Environment)
        });
        if (hookErrors.Count > 0)
            return ServiceResult<ServiceOperationStart>.ValidationFailure(hookErrors);

        var server = await _serverRepository.FirstOrDefaultAsync(s => s.Id == dto.ServerId, cancellationToken);
        if (server is null)
            return ServiceResult<ServiceOperationStart>.ValidationFailure(nameof(dto.ServerId), "Sunucu bulunamadı.");

        if (await _repository.NameExistsAsync(dto.ServerId, dto.Name!, null, cancellationToken))
            return ServiceResult<ServiceOperationStart>.ValidationFailure(nameof(dto.Name), "Bu sunucuda aynı adlı bir servis var.");

        var conflicts = await PortConflictsAsync(dto.ServerId, Guid.Empty, dto.Ports, cancellationToken);
        if (conflicts.Count > 0)
            return ServiceResult<ServiceOperationStart>.ValidationFailure(conflicts);

        var slug = ManagedServiceNames.Slugify(dto.Name);
        for (var number = 2; await _repository.SlugExistsAsync(dto.ServerId, slug, cancellationToken); number++)
            slug = ManagedServiceNames.WithSuffix(ManagedServiceNames.Slugify(dto.Name), number);

        var credentials = new ServiceCredentials
        {
            Username = template.Credentials.HasUsername ? dto.Username : null,
            Password = template.Credentials.HasPassword ? dto.Password : null,
            Database = template.Credentials.HasDatabase ? dto.Database : null,
            EncryptionKey = template.Credentials.GeneratesEncryptionKey ? ServiceSecrets.GenerateKey() : null
        };

        var service = new ManagedService
        {
            ServerId = dto.ServerId,
            Name = dto.Name!,
            Slug = slug,
            TemplateKey = template.Key,
            ImageTag = dto.ImageTag!,
            ContainerName = ManagedServiceNames.ContainerName(slug),
            EncryptedCredentials = _secretProtector.Protect(credentials.ToJson()),
            VolumeMode = dto.VolumeMode,
            HostDataPath = dto.VolumeMode == ManagedServiceVolumeMode.HostPath ? dto.HostDataPath : null,
            Status = ManagedServiceStatus.Installing,
            CreatedAt = UtcNow,
            CreatedBy = actor.UserName
        };
        ApplySettings(service, dto);

        var operation = NewOperation(service, ManagedServiceOperationKind.Install, actor, toTag: service.ImageTag);
        await _repository.AddAsync(service, cancellationToken);
        await _repository.AddOperationAsync(operation, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.ManagedServiceCreate, service,
            $"{template.DisplayName} {service.ImageTag} ({server.Name}) | {DescribeSettings(service, template)}", true, actor, cancellationToken);
        _logger.LogInformation("Servis kurulumu başlatıldı. ServiceId: {ServiceId}, Template: {Template}", service.Id, template.Key);
        return ServiceResult<ServiceOperationStart>.Success(new ServiceOperationStart(service.Id, operation.Id), "Kurulum başlatıldı.");
    }

    public async Task<ServiceResult<ServiceOperationStart>> BeginRecreateAsync(Guid id, UpdateManagedServiceDto dto, ServiceActor actor, CancellationToken cancellationToken = default)
    {
        var (service, template, error) = await LoadForChangeAsync(id, cancellationToken);
        if (error is not null)
            return error;

        dto.Id = id;
        var context = new ValidationContext<UpdateManagedServiceDto>(dto);
        context.RootContextData[UpdateManagedServiceDtoValidator.TemplateKey] = template!;
        var validation = await _updateValidator.ValidateAsync(context, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<ServiceOperationStart>.ValidationFailure(validation);

        TryReadCredentials(service!, out var current);
        var hookErrors = ValidateWithHooks(template!, new ServiceTemplateFormContext
        {
            TemplateKey = template!.Key,
            IsInstall = false,
            Name = service!.Name,
            ImageTag = service.ImageTag,
            Username = current.Username,
            Database = current.Database,
            Environment = EnvironmentDictionary(dto.Environment)
        });
        if (hookErrors.Count > 0)
            return ServiceResult<ServiceOperationStart>.ValidationFailure(hookErrors);

        var conflicts = await PortConflictsAsync(service.ServerId, service.Id, dto.Ports, cancellationToken);
        if (conflicts.Count > 0)
            return ServiceResult<ServiceOperationStart>.ValidationFailure(conflicts);

        ApplySettings(service, dto);
        service.Status = ManagedServiceStatus.Updating;
        service.UpdatedAt = UtcNow;
        service.UpdatedBy = actor.UserName;

        var operation = NewOperation(service, ManagedServiceOperationKind.Recreate, actor, service.ImageTag, service.ImageTag);
        await _repository.AddOperationAsync(operation, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.ManagedServiceRecreate, service, DescribeSettings(service, template!), true, actor, cancellationToken);
        return ServiceResult<ServiceOperationStart>.Success(new ServiceOperationStart(service.Id, operation.Id), "Ayarlar kaydedildi; servis yeniden oluşturuluyor.");
    }

    public async Task<ServiceResult<ServiceOperationStart>> BeginUpgradeAsync(Guid id, UpgradeManagedServiceDto dto, ServiceActor actor, CancellationToken cancellationToken = default)
    {
        dto.ImageTag = dto.ImageTag?.Trim();
        var validation = await _upgradeValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<ServiceOperationStart>.ValidationFailure(validation);

        var (service, template, error) = await LoadForChangeAsync(id, cancellationToken);
        if (error is not null)
            return error;

        if (MajorUpgradeWarning(template!, service!.ImageTag, dto.ImageTag!) is { } warning && !dto.ConfirmMajorUpgrade)
            return ServiceResult<ServiceOperationStart>.ValidationFailure(nameof(dto.ConfirmMajorUpgrade), warning);

        service.Status = ManagedServiceStatus.Updating;
        service.UpdatedAt = UtcNow;
        service.UpdatedBy = actor.UserName;
        var operation = NewOperation(service, ManagedServiceOperationKind.Upgrade, actor, service.ImageTag, dto.ImageTag);
        await _repository.AddOperationAsync(operation, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.ManagedServiceUpgrade, service, $"{service.ImageTag} → {dto.ImageTag}", true, actor, cancellationToken);
        return ServiceResult<ServiceOperationStart>.Success(new ServiceOperationStart(service.Id, operation.Id), "Sürüm yükseltme başlatıldı.");
    }

    public async Task<ServiceResult<ServiceOperationStart>> BeginRemoveAsync(Guid id, RemoveManagedServiceDto dto, ServiceActor actor, CancellationToken cancellationToken = default)
    {
        var service = await _repository.GetAsync(id, cancellationToken);
        if (service is null || service.Status == ManagedServiceStatus.Removed)
            return ServiceResult<ServiceOperationStart>.NotFound(NotFoundMessage);

        if (!string.Equals(dto.ConfirmationName?.Trim(), service.Name, StringComparison.Ordinal))
            return ServiceResult<ServiceOperationStart>.ValidationFailure(nameof(dto.ConfirmationName), "Onay için servis adını aynen yazın.");

        if (await _repository.GetRunningOperationAsync(id, cancellationToken) is not null)
            return ServiceResult<ServiceOperationStart>.Failure(BusyMessage, ServiceErrorType.Conflict);

        service.Status = ManagedServiceStatus.Removing;
        service.UpdatedAt = UtcNow;
        service.UpdatedBy = actor.UserName;
        var operation = NewOperation(service, ManagedServiceOperationKind.Remove, actor, service.ImageTag, null);
        operation.RemoveData = dto.RemoveData;
        await _repository.AddOperationAsync(operation, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.ManagedServiceRemove, service, dto.RemoveData ? "Veri de silinecek" : "Veri korunacak", true, actor, cancellationToken);
        return ServiceResult<ServiceOperationStart>.Success(new ServiceOperationStart(service.Id, operation.Id), "Servis kaldırılıyor.");
    }

    // ---------------------------------------------------------------- İşlem yürütme

    public async Task<ServiceResult> RunOperationAsync(Guid operationId, ServiceActor actor, IServiceOperationObserver observer, CancellationToken cancellationToken)
    {
        var operation = await _repository.GetOperationAsync(operationId, CancellationToken.None);
        if (operation is null || operation.Status != ManagedServiceOperationStatus.Running)
            return ServiceResult.NotFound("Başlamayı bekleyen işlem bulunamadı.");

        var service = await _repository.GetAsync(operation.ServiceId, CancellationToken.None);
        if (service is null)
        {
            operation.Status = ManagedServiceOperationStatus.Failed;
            operation.FailureReason = NotFoundMessage;
            operation.FinishedAt = UtcNow;
            await _repository.SaveChangesAsync(CancellationToken.None);
            return ServiceResult.NotFound(NotFoundMessage);
        }

        TryReadCredentials(service, out var credentials);
        var environment = TryReadEnvironment(service);
        var secrets = credentials.Secrets()
            .Concat(ManagedServiceSettingsRules.FromEnvironmentText(environment).Select(e => e.Value ?? string.Empty).Where(v => v.Length >= 8));
        var recorder = new ServiceOperationRecorder(
            observer,
            new SecretMasker(secrets),
            Math.Max(64, _options.MaxStoredLogKilobytes) * 1024,
            (log, ct) => _repository.UpdateOperationLogAsync(operationId, log, ct),
            async (stage, ct) =>
            {
                if (stage is ServiceOperationStage.Failed)
                    return;
                operation.Stage = stage.ToString();
                await _repository.SaveChangesAsync(ct);
            },
            _timeProvider);

        ServiceOperationResult result;
        try
        {
            result = operation.Kind == ManagedServiceOperationKind.Remove
                ? await ExecuteRemoveAsync(service, operation, recorder, cancellationToken)
                : await ExecuteDeployAsync(service, operation, credentials, environment, recorder, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await CompleteAsync(service, operation, recorder, ServiceOperationResult.Failed(InterruptedReason), actor, interrupted: true);
            return ServiceResult.Failure(InterruptedReason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Servis işlemi beklenmeyen hata ile bitti. OperationId: {OperationId}", operationId);
            result = ServiceOperationResult.Failed("Beklenmeyen bir hata oluştu; ayrıntılar uygulama loglarında.");
        }

        if (cancellationToken.IsCancellationRequested && !result.Succeeded)
        {
            await CompleteAsync(service, operation, recorder, ServiceOperationResult.Failed(InterruptedReason), actor, interrupted: true);
            return ServiceResult.Failure(InterruptedReason);
        }

        await CompleteAsync(service, operation, recorder, result, actor, interrupted: false);
        return result.Succeeded
            ? ServiceResult.Success(SuccessMessage(operation.Kind))
            : ServiceResult.Failure(result.FailureReason ?? "İşlem başarısız oldu.");
    }

    public Task<int> InterruptRunningAsync(CancellationToken cancellationToken = default) =>
        _repository.InterruptRunningAsync(UtcNow, InterruptedReason, cancellationToken);

    private async Task<ServiceOperationResult> ExecuteDeployAsync(
        ManagedService service,
        ManagedServiceOperation operation,
        ServiceCredentials credentials,
        string? environment,
        ServiceOperationRecorder recorder,
        CancellationToken cancellationToken)
    {
        var resolution = _templates.Resolve(service.TemplateKey);
        var template = resolution.IsAvailable ? resolution.Template : null;
        if (template is null)
            return ServiceOperationResult.Failed(resolution.BlockedMessage ?? $"\"{service.TemplateKey}\" şablonu bu sürümde bulunmuyor.");

        if (string.IsNullOrEmpty(service.EncryptedCredentials) || !TryReadCredentials(service, out _))
            return ServiceOperationResult.Failed(DecryptFailedMessage);

        var tag = operation.ToTag ?? service.ImageTag;
        await recorder.InfoAsync($"{template.DisplayName} {tag} → {service.ContainerName} ({service.Server?.Name ?? service.ServerId.ToString()})", cancellationToken);

        var hooks = _templates.GetHooks(template.Key);
        if (!TryBuildExtraArguments(hooks, template, credentials, environment, tag, out var extraArguments, out var hookError))
            return ServiceOperationResult.Failed(hookError!);

        var connection = await _connectionProvider.GetAsync(service.ServerId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceOperationResult.Failed(connection.Message ?? "Sunucuya bağlanılamadı.");

        var plan = BuildPlan(service, template, credentials, environment, tag, operation.Kind != ManagedServiceOperationKind.Install, extraArguments);
        var run = await _provider.DeployAsync(connection.Data!.Context, plan, recorder, cancellationToken);
        if (!run.IsSuccess)
            return ServiceOperationResult.Failed(run.Message ?? "İşlem çalıştırılamadı.");

        if (run.Data!.Succeeded && operation.Kind == ManagedServiceOperationKind.Install && hooks.Count > 0)
            await RunAfterInstallHooksAsync(hooks, service, template, credentials, plan, recorder, cancellationToken);

        return run.Data;
    }

    /// <summary>Etkin eklenti kancalarının ek container argümanları; geçersiz argüman veya kanca hatası işlemi durdurur.</summary>
    private bool TryBuildExtraArguments(
        IReadOnlyList<IServiceTemplateHooks> hooks,
        ServiceTemplate template,
        ServiceCredentials credentials,
        string? environment,
        string tag,
        out IReadOnlyList<string> arguments,
        out string? error)
    {
        var result = new List<string>();
        arguments = result;
        error = null;
        if (hooks.Count == 0)
            return true;

        var context = new ServiceTemplateCommandContext
        {
            TemplateKey = template.Key,
            ImageTag = tag,
            Environment = EnvironmentDictionary(environment)
        };
        var secrets = credentials.Secrets().ToList();
        foreach (var hook in hooks)
        {
            IReadOnlyList<string> extra;
            try
            {
                extra = hook.BuildExtraArgs(context) ?? [];
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Şablon kancası argüman üretemedi. Template: {Template}, Hook: {Hook}", template.Key, hook.GetType().FullName);
                error = "Şablon eklentisi container argümanlarını üretemedi; ayrıntılar uygulama logunda.";
                return false;
            }

            if (ServiceTemplateValidator.ValidateExtraArguments(extra, secrets) is { } invalid)
            {
                error = invalid;
                return false;
            }

            result.AddRange(extra);
        }

        if (result.Count + template.Command.Count > ServiceTemplateValidator.MaxCommandArguments * 2)
        {
            error = "Container argümanları çok uzun.";
            return false;
        }

        return true;
    }

    private async Task RunAfterInstallHooksAsync(
        IReadOnlyList<IServiceTemplateHooks> hooks,
        ManagedService service,
        ServiceTemplate template,
        ServiceCredentials credentials,
        ManagedServicePlan plan,
        ServiceOperationRecorder recorder,
        CancellationToken cancellationToken)
    {
        var context = new ServiceTemplateInstallContext
        {
            ServiceId = service.Id,
            ServerId = service.ServerId,
            Name = service.Name,
            TemplateKey = template.Key,
            ImageTag = plan.Tag,
            ContainerName = service.ContainerName,
            InternalEndpoint = template.PrimaryPort is { } primary ? new ServiceEndpoint(service.ContainerName, primary.ContainerPort) : null,
            PublishedPorts = plan.Ports,
            Credentials = credentials
        };
        var log = new HookLog(recorder);
        foreach (var hook in hooks)
        {
            try
            {
                await hook.AfterInstallAsync(context, log, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Şablon kancası kurulum sonrası adımda hata verdi. Template: {Template}, Hook: {Hook}", template.Key, hook.GetType().FullName);
                await recorder.OnOutputAsync(ServiceConsole.Warning("Şablon eklentisinin kurulum sonrası adımı başarısız oldu; servis çalışıyor. Ayrıntılar uygulama logunda."), cancellationToken);
            }
        }
    }

    /// <summary>Eklenti kancalarının form doğrulaması; hatalar form alanlarına yazılır.</summary>
    private List<ServiceError> ValidateWithHooks(ServiceTemplate template, ServiceTemplateFormContext context)
    {
        var errors = new List<ServiceError>();
        foreach (var hook in _templates.GetHooks(template.Key))
        {
            try
            {
                foreach (var error in hook.Validate(context) ?? [])
                    errors.Add(new ServiceError(string.IsNullOrEmpty(error.Property) ? string.Empty : error.Property, error.Message));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Şablon kancası doğrulamada hata verdi. Template: {Template}, Hook: {Hook}", template.Key, hook.GetType().FullName);
                errors.Add(new ServiceError(string.Empty, "Şablon eklentisi formu doğrulayamadı; ayrıntılar uygulama logunda."));
            }
        }

        return errors;
    }

    private static Dictionary<string, string> EnvironmentDictionary(string? environment)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in ManagedServiceSettingsRules.FromEnvironmentText(environment))
            result[entry.Key!] = entry.Value ?? string.Empty;
        return result;
    }

    private static Dictionary<string, string> EnvironmentDictionary(IEnumerable<EnvironmentEntryDto> entries) =>
        EnvironmentDictionary(ManagedServiceSettingsRules.ToEnvironmentText(entries));

    private sealed class HookLog(ServiceOperationRecorder recorder) : IServiceTemplateHookLog
    {
        public Task InfoAsync(string message, CancellationToken cancellationToken) =>
            recorder.OnOutputAsync(ServiceConsole.Info(Clean(message)), cancellationToken);

        public Task WarningAsync(string message, CancellationToken cancellationToken) =>
            recorder.OnOutputAsync(ServiceConsole.Warning(Clean(message)), cancellationToken);

        /// <summary>Eklenti iletisinden kontrol karakterleri (ANSI kaçışları dahil) atılır; satırlar tek satıra indirilir.</summary>
        private static string Clean(string? message)
        {
            var text = new string((message ?? string.Empty).Select(c => char.IsControl(c) ? ' ' : c).ToArray()).Trim();
            return text.Length > 2000 ? text[..2000] : text;
        }
    }

    private async Task<ServiceOperationResult> ExecuteRemoveAsync(
        ManagedService service,
        ManagedServiceOperation operation,
        ServiceOperationRecorder recorder,
        CancellationToken cancellationToken)
    {
        var connection = await _connectionProvider.GetAsync(service.ServerId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceOperationResult.Failed(connection.Message ?? "Sunucuya bağlanılamadı.");

        var plan = new ManagedServiceRemovalPlan
        {
            Slug = service.Slug,
            ContainerName = service.ContainerName,
            RemoveData = operation.RemoveData,
            VolumeMode = service.VolumeMode,
            HostDataPath = service.HostDataPath,
            CommandTimeout = TimeSpan.FromSeconds(Math.Max(30, _options.CommandTimeoutSeconds))
        };
        var run = await _provider.RemoveAsync(connection.Data!.Context, plan, recorder, cancellationToken);
        return run.IsSuccess ? run.Data! : ServiceOperationResult.Failed(run.Message ?? "Kaldırma çalıştırılamadı.");
    }

    private async Task CompleteAsync(
        ManagedService service,
        ManagedServiceOperation operation,
        ServiceOperationRecorder recorder,
        ServiceOperationResult result,
        ServiceActor actor,
        bool interrupted)
    {
        var message = result.Succeeded ? SuccessMessage(operation.Kind) : result.FailureReason ?? "İşlem başarısız oldu.";
        try
        {
            await recorder.FlushPendingAsync(CancellationToken.None);
            await recorder.OnOutputAsync(result.Succeeded ? ServiceConsole.Success(message) : ServiceConsole.Error(message), CancellationToken.None);
            await recorder.FlushPendingAsync(CancellationToken.None);
            await recorder.OnStageAsync(result.Succeeded ? ServiceOperationStage.Completed : ServiceOperationStage.Failed,
                result.Succeeded ? "Tamamlandı" : "Başarısız", CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Servis işlem sonucu izleyiciye iletilemedi. OperationId: {OperationId}", operation.Id);
        }

        var now = UtcNow;
        operation.Status = interrupted
            ? ManagedServiceOperationStatus.Interrupted
            : result.Succeeded ? ManagedServiceOperationStatus.Succeeded : ManagedServiceOperationStatus.Failed;
        operation.FailureReason = result.Succeeded ? null : TextHelper.Truncate(message, 1000);
        operation.FinishedAt = now;
        operation.Log = recorder.Log;

        if (operation.Kind == ManagedServiceOperationKind.Remove && result.Succeeded)
        {
            service.Status = ManagedServiceStatus.Removed;
            service.IsDeleted = true;
            service.DeletedAt = now;
            service.DeletedBy = actor.UserName;
            service.LastError = null;
        }
        else if (result.Succeeded)
        {
            service.Status = ManagedServiceStatus.Running;
            service.LastError = null;
            if (operation.Kind == ManagedServiceOperationKind.Upgrade && operation.ToTag is not null)
                service.ImageTag = operation.ToTag;
        }
        else
        {
            service.Status = ManagedServiceStatus.Failed;
            service.LastError = TextHelper.Truncate(message, 1000);
        }

        service.UpdatedAt = now;
        await _repository.SaveChangesAsync(CancellationToken.None);

        await AuditAsync(AuditActions.ManagedServiceOperationComplete, service,
            $"{KindText(operation.Kind)}: {message}", result.Succeeded, actor, CancellationToken.None);
    }

    // ---------------------------------------------------------------- Yardımcılar

    /// <summary>Kayıtlı ayarlardan sunucuda uygulanacak planı üretir.</summary>
    public ManagedServicePlan BuildPlan(
        ManagedService service,
        ServiceTemplate template,
        ServiceCredentials credentials,
        string? environment,
        string tag,
        bool replaceExisting,
        IReadOnlyList<string>? extraArguments = null)
    {
        var published = ServicePortBindings.Published(template, ServicePortBindings.FromJson(service.PortBindings), service.ExposePublicly);
        ServiceValidation.TryParseNetworks(service.Networks, out var extraNetworks, out _);
        var networks = NetworksOf(service);
        var managedNetworks = service.JoinProxyNetwork
            ? new[] { ManagedServiceNames.ServicesNetwork, ManagedServiceNames.ProxyNetwork }
            : new[] { ManagedServiceNames.ServicesNetwork };

        var secrets = credentials.Secrets()
            .Concat(ManagedServiceSettingsRules.FromEnvironmentText(environment).Select(e => e.Value ?? string.Empty).Where(v => v.Length >= 8))
            .ToList();

        return new ManagedServicePlan
        {
            Slug = service.Slug,
            ContainerName = service.ContainerName,
            TemplateKey = template.Key,
            Image = template.Image,
            Tag = tag,
            EnvironmentFile = ManagedServiceSettingsRules.BuildEnvironmentFile(template, credentials, environment),
            Ports = published,
            VolumeMode = service.VolumeMode,
            DataPath = template.DataPath(tag),
            HostDataPath = service.HostDataPath,
            DataOwner = template.DataOwner,
            MemoryLimitMb = service.MemoryLimitMb,
            CpuLimit = service.CpuLimit,
            Networks = networks,
            ManagedNetworks = managedNetworks,
            Command = extraArguments is { Count: > 0 } ? [.. template.Command, .. extraArguments] : template.Command,
            HealthCommand = template.HealthCommand,
            ReadinessCommand = template.ReadinessCommand,
            Firewall = FirewallPlan(service, template),
            RequiresX86 = template.RequiresX86,
            ReplaceExisting = replaceExisting,
            Secrets = secrets,
            PullTimeout = TimeSpan.FromMinutes(Math.Max(1, _options.PullTimeoutMinutes)),
            CommandTimeout = TimeSpan.FromSeconds(Math.Max(30, _options.CommandTimeoutSeconds)),
            HealthTimeout = TimeSpan.FromSeconds(Math.Max(Math.Max(30, _options.HealthTimeoutSeconds), template.HealthTimeoutSeconds))
        };
    }

    /// <summary>Port dışarıya açık ve izin listesi doluysa kısıtlama kuralları; aksi halde kural yazılmaz (eskiler silinir).</summary>
    public static ServiceFirewallPlan FirewallPlan(ManagedService service, ServiceTemplate template)
    {
        var tag = ManagedServiceNames.FirewallTag(service.Slug);
        if (!service.ExposePublicly || !ServiceValidation.TryParseCidrs(service.AllowedSourceIps, out var cidrs, out _) || cidrs.Count == 0)
            return ServiceFirewallPlan.None(tag);

        var ports = ServicePortBindings.Published(template, ServicePortBindings.FromJson(service.PortBindings), true)
            .Select(p => p.HostPort)
            .Distinct()
            .ToList();
        return new ServiceFirewallPlan(tag, ports, cidrs);
    }

    /// <summary>Ana sürüm değişiyorsa kullanıcıya gösterilecek uyarı; değişmiyorsa null.</summary>
    public static string? MajorUpgradeWarning(ServiceTemplate template, string fromTag, string toTag)
    {
        if (!template.WarnOnMajorUpgrade)
            return null;

        var from = ServiceTemplates.MajorVersion(fromTag);
        var to = ServiceTemplates.MajorVersion(toTag);
        if (from is null || to is null)
        {
            return string.Equals(fromTag, toTag, StringComparison.Ordinal)
                ? null
                : $"{fromTag} → {toTag}: sürüm numarası okunamadı; ana sürüm değişebilir. Veri dosyaları yeni sürümle uyumsuz olabilir; önce yedek alın.";
        }

        if (from == to)
            return null;

        return to < from
            ? $"{fromTag} → {toTag} bir ana sürüm düşürmesidir. Veritabanları eski sürüme dönüşü desteklemez; veri açılmayabilir. Yalnızca yedekten geri dönüyorsanız onaylayın."
            : $"{fromTag} → {toTag} bir ana sürüm yükseltmesidir. {template.DisplayName} veri dosyalarını otomatik dönüştürmeyebilir; önce yedek alın ve sürüm notlarını okuyun.";
    }

    private async Task<(ManagedService? Service, ServiceTemplate? Template, ServiceResult<ServiceOperationStart>? Error)> LoadForChangeAsync(Guid id, CancellationToken cancellationToken)
    {
        var service = await _repository.GetAsync(id, cancellationToken);
        if (service is null || service.Status == ManagedServiceStatus.Removed)
            return (null, null, ServiceResult<ServiceOperationStart>.NotFound(NotFoundMessage));

        // Şablon eklentisi devre dışı/kaldırılmışsa yeniden oluşturma ve yükseltme engellenir; kayıtlı TemplateKey korunur.
        var resolution = _templates.Resolve(service.TemplateKey);
        if (!resolution.IsAvailable || resolution.Template is null)
            return (null, null, ServiceResult<ServiceOperationStart>.Failure(resolution.BlockedMessage ?? "Servis şablonu bulunamadı.", ServiceErrorType.Conflict));

        var template = resolution.Template;

        if (service.Status == ManagedServiceStatus.Removing || await _repository.GetRunningOperationAsync(id, cancellationToken) is not null)
            return (null, null, ServiceResult<ServiceOperationStart>.Failure(BusyMessage, ServiceErrorType.Conflict));

        return (service, template, null);
    }

    private async Task<List<ServiceError>> PortConflictsAsync(Guid serverId, Guid excludeId, IReadOnlyList<ServicePortFormItem> ports, CancellationToken cancellationToken)
    {
        var errors = new List<ServiceError>();
        if (!ports.Any(p => p.Publish && p.HostPort is not null))
            return errors;

        var used = new Dictionary<int, string>();
        foreach (var other in await _repository.ListOthersOnServerAsync(serverId, excludeId, cancellationToken))
        {
            foreach (var binding in ServicePortBindings.FromJson(other.PortBindings).Where(b => b.HostPort is not null))
                used.TryAdd(binding.HostPort!.Value, other.Name);
        }

        for (var i = 0; i < ports.Count; i++)
        {
            if (ports[i].Publish && ports[i].HostPort is { } hostPort && used.TryGetValue(hostPort, out var owner))
                errors.Add(new ServiceError($"Ports[{i}].HostPort", $"{hostPort.ToString(CultureInfo.InvariantCulture)} portu \"{owner}\" servisi tarafından kullanılıyor."));
        }

        return errors;
    }

    private void ApplySettings(ManagedService service, ManagedServiceSettingsDto dto)
    {
        service.PortBindings = ServicePortBindings.ToJson(dto.Ports
            .Select(p => new ServicePortBinding(p.ContainerPort, p.Publish ? p.HostPort : null)));
        service.ExposePublicly = dto.ExposePublicly;
        ServiceValidation.TryParseCidrs(dto.AllowedSourceIps, out var cidrs, out _);
        service.AllowedSourceIps = cidrs.Count == 0 ? null : string.Join('\n', cidrs);
        var environment = ManagedServiceSettingsRules.ToEnvironmentText(dto.Environment);
        service.EncryptedEnvironment = environment is null ? null : _secretProtector.Protect(environment);
        service.MemoryLimitMb = dto.MemoryLimitMb;
        service.CpuLimit = dto.CpuLimit;
        service.JoinProxyNetwork = dto.JoinProxyNetwork;
        ServiceValidation.TryParseNetworks(dto.Networks, out var networks, out _);
        service.Networks = networks.Count == 0 ? null : string.Join(',', networks);
    }

    private ManagedServiceOperation NewOperation(ManagedService service, ManagedServiceOperationKind kind, ServiceActor actor, string? fromTag = null, string? toTag = null) => new()
    {
        ServiceId = service.Id,
        ServerId = service.ServerId,
        ServiceName = service.Name,
        Kind = kind,
        Status = ManagedServiceOperationStatus.Running,
        Stage = ServiceOperationStage.Docker.ToString(),
        FromTag = fromTag,
        ToTag = toTag,
        UserId = actor.UserId,
        UserName = actor.UserName,
        IpAddress = actor.IpAddress,
        StartedAt = UtcNow
    };

    private async Task<ServiceResult<T>> WithServiceAsync<T>(
        Guid id,
        Func<ManagedService, RemoteExecutionContext, CancellationToken, Task<ServiceResult<T>>> query,
        CancellationToken cancellationToken)
    {
        var service = await _repository.GetAsync(id, cancellationToken);
        if (service is null)
            return ServiceResult<T>.NotFound(NotFoundMessage);

        var connection = await _connectionProvider.GetAsync(service.ServerId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult<T>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        return await query(service, connection.Data!.Context, cancellationToken);
    }

    private bool TryReadCredentials(ManagedService service, out ServiceCredentials credentials)
    {
        credentials = new ServiceCredentials();
        if (string.IsNullOrEmpty(service.EncryptedCredentials))
            return true;

        try
        {
            credentials = ServiceCredentials.FromJson(_secretProtector.Unprotect(service.EncryptedCredentials));
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or System.Text.Json.JsonException or FormatException)
        {
            _logger.LogError(ex, "Servis kimlik bilgileri çözülemedi. ServiceId: {ServiceId}", service.Id);
            return false;
        }
    }

    private string? TryReadEnvironment(ManagedService service)
    {
        if (string.IsNullOrEmpty(service.EncryptedEnvironment))
            return null;

        try
        {
            return _secretProtector.Unprotect(service.EncryptedEnvironment);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            _logger.LogError(ex, "Servis ortam değişkenleri çözülemedi. ServiceId: {ServiceId}", service.Id);
            return null;
        }
    }

    private static IReadOnlyList<string> NetworksOf(ManagedService service)
    {
        var networks = new List<string> { ManagedServiceNames.ServicesNetwork };
        if (service.JoinProxyNetwork)
            networks.Add(ManagedServiceNames.ProxyNetwork);
        if (ServiceValidation.TryParseNetworks(service.Networks, out var extra, out _))
            networks.AddRange(extra);
        return networks;
    }

    private ManagedServiceListItemDto ToListItem(ManagedService service)
    {
        var resolution = _templates.Resolve(service.TemplateKey);
        var template = resolution.Template;
        var ports = template is null
            ? []
            : ServicePortBindings.Published(template, ServicePortBindings.FromJson(service.PortBindings), service.ExposePublicly);
        var address = service.Server?.IpAddress ?? string.Empty;
        string? webUrl = null;
        if (service.ExposePublicly && template?.WebUiPort is { } web && ports.FirstOrDefault(p => p.ContainerPort == web.ContainerPort) is { } webPort && address.Length > 0)
            webUrl = ServiceConnectionStrings.Http(new ServiceEndpoint(address, webPort.HostPort));

        return new ManagedServiceListItemDto
        {
            Id = service.Id,
            ServerId = service.ServerId,
            ServerName = service.Server?.Name ?? string.Empty,
            ServerAddress = address,
            Name = service.Name,
            Slug = service.Slug,
            TemplateKey = service.TemplateKey,
            Template = template,
            TemplateAvailability = resolution.Availability,
            TemplateBadge = resolution.Badge,
            TemplateBlockedMessage = resolution.BlockedMessage,
            ImageTag = service.ImageTag,
            ContainerName = service.ContainerName,
            Status = service.Status,
            LastError = service.LastError,
            ExposePublicly = service.ExposePublicly,
            HasAllowList = !string.IsNullOrWhiteSpace(service.AllowedSourceIps),
            Ports = ports,
            WebUiUrl = webUrl,
            CreatedAt = service.CreatedAt
        };
    }

    private static ManagedServiceOperationDto ToOperationDto(ManagedServiceOperation operation, bool includeLog) => new()
    {
        Id = operation.Id,
        ServiceId = operation.ServiceId,
        ServerId = operation.ServerId,
        ServiceName = operation.ServiceName,
        Kind = operation.Kind,
        Status = operation.Status,
        Stage = operation.Stage,
        FromTag = operation.FromTag,
        ToTag = operation.ToTag,
        RemoveData = operation.RemoveData,
        FailureReason = operation.FailureReason,
        Log = includeLog ? operation.Log : string.Empty,
        UserName = operation.UserName,
        IpAddress = operation.IpAddress,
        StartedAt = operation.StartedAt,
        FinishedAt = operation.FinishedAt
    };

    private static string DescribeSettings(ManagedService service, ServiceTemplate template)
    {
        var ports = ServicePortBindings.Published(template, ServicePortBindings.FromJson(service.PortBindings), service.ExposePublicly);
        var portText = ports.Count == 0 ? "port yayınlanmadı" : string.Join(", ", ports.Select(p => $"{p.BindAddress}:{p.HostPort}->{p.ContainerPort}"));
        var allow = string.IsNullOrWhiteSpace(service.AllowedSourceIps) ? string.Empty : $" | İzinli: {service.AllowedSourceIps.Replace('\n', ' ')}";
        var limits = service.MemoryLimitMb is null && service.CpuLimit is null
            ? string.Empty
            : $" | Bellek: {service.MemoryLimitMb?.ToString(CultureInfo.InvariantCulture) ?? "-"} MB, CPU: {service.CpuLimit?.ToString(CultureInfo.InvariantCulture) ?? "-"}";
        return $"Portlar: {portText}{allow}{limits} | Ağlar: {string.Join(", ", NetworksOf(service))}";
    }

    private static string SuccessMessage(ManagedServiceOperationKind kind) => kind switch
    {
        ManagedServiceOperationKind.Install => "Servis kuruldu ve bağlantı testi başarılı.",
        ManagedServiceOperationKind.Recreate => "Servis yeni ayarlarla yeniden oluşturuldu.",
        ManagedServiceOperationKind.Upgrade => "Servis yeni sürüme yükseltildi.",
        ManagedServiceOperationKind.Remove => "Servis kaldırıldı.",
        _ => "İşlem tamamlandı."
    };

    public static string KindText(ManagedServiceOperationKind kind) => kind switch
    {
        ManagedServiceOperationKind.Install => "Kurulum",
        ManagedServiceOperationKind.Recreate => "Yeniden oluşturma",
        ManagedServiceOperationKind.Upgrade => "Sürüm yükseltme",
        ManagedServiceOperationKind.Remove => "Kaldırma",
        _ => kind.ToString()
    };

    private Task AuditAsync(string action, ManagedService service, string details, bool isSuccess, ServiceActor? actor, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(
            action,
            AuditEntityTypes.ManagedService,
            service.Id.ToString(),
            service.Name,
            $"Sunucu: {service.Server?.Name ?? service.ServerId.ToString()} | Container: {service.ContainerName} | {details}",
            isSuccess,
            UserNameOverride: actor?.UserName,
            UserIdOverride: actor?.UserId,
            IpAddressOverride: actor?.IpAddress), cancellationToken);
}
