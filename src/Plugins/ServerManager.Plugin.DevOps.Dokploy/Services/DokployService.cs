using System.Security.Cryptography;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Domain.Entities;
using ServerManager.Plugin.DevOps.Dokploy.Core;
using ServerManager.Plugin.DevOps.Dokploy.Domain;
using ServerManager.Plugin.DevOps.Dokploy.DTOs;

namespace ServerManager.Plugin.DevOps.Dokploy.Services;

public class DokployService : IDokployService
{
    private const string NotDetectedMessage = "Bu sunucuda Dokploy bulunamadı.";
    private const string ApiKeyRequiredMessage = "Projeleri görmek için Dokploy API anahtarını kaydedin.";
    private const string InterruptedReason = "Uygulama yeniden başladığı için kurulum takibi kesildi; sunucudaki durumu Dokploy sekmesinden kontrol edin.";
    private static readonly TimeSpan HealthPollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan HealthProgressInterval = TimeSpan.FromSeconds(15);

    private readonly IServerRepository _serverRepository;
    private readonly IServerConnectionProvider _connectionProvider;
    private readonly IDokployRepository _dokployRepository;
    private readonly IDokployProvider _provider;
    private readonly IDokployApiClient _apiClient;
    private readonly ISecretProtector _secretProtector;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<DokploySettingsDto> _settingsValidator;
    private readonly IValidator<DokployInstallRequestDto> _installValidator;
    private readonly DokployOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DokployService> _logger;

    public DokployService(
        IServerRepository serverRepository,
        IServerConnectionProvider connectionProvider,
        IDokployRepository dokployRepository,
        IDokployProvider provider,
        IDokployApiClient apiClient,
        ISecretProtector secretProtector,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IValidator<DokploySettingsDto> settingsValidator,
        IValidator<DokployInstallRequestDto> installValidator,
        IOptions<DokployOptions> options,
        TimeProvider timeProvider,
        ILogger<DokployService> logger)
    {
        _serverRepository = serverRepository;
        _connectionProvider = connectionProvider;
        _dokployRepository = dokployRepository;
        _provider = provider;
        _apiClient = apiClient;
        _secretProtector = secretProtector;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _settingsValidator = settingsValidator;
        _installValidator = installValidator;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public async Task<ServiceResult<DokployOverviewDto>> GetOverviewAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdAsync(serverId, cancellationToken);
        if (server is null)
            return ServiceResult<DokployOverviewDto>.NotFound("Sunucu bulunamadı.");

        var instance = await _dokployRepository.GetByServerIdAsync(serverId, cancellationToken);
        var refresh = await RefreshAsync(server, instance, _currentUser.UserName, cancellationToken);
        var installations = await _dokployRepository.GetInstallationsAsync(serverId, Math.Max(1, _options.InstallationHistoryCount), cancellationToken);

        return ServiceResult<DokployOverviewDto>.Success(new DokployOverviewDto
        {
            Instance = refresh.Instance?.ToDto(),
            Host = refresh.Host,
            HostError = refresh.HostError,
            Installations = installations.Select(i => i.ToDto()).ToList(),
            RunningInstallationId = installations.FirstOrDefault(i => i.Status == DokployInstallationStatus.Running)?.Id
        });
    }

    public async Task<ServiceResult<DokployCompatibilityReportDto>> CheckCompatibilityAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult<DokployCompatibilityReportDto>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        return await EvaluateCompatibilityAsync(connection.Data!.Context, cancellationToken);
    }

    public async Task<ServiceResult<DokployHealthResultDto>> RunHealthCheckAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdAsync(serverId, cancellationToken);
        if (server is null)
            return ServiceResult<DokployHealthResultDto>.NotFound("Sunucu bulunamadı.");

        var instance = await _dokployRepository.GetByServerIdAsync(serverId, cancellationToken);
        var refresh = await RefreshAsync(server, instance, _currentUser.UserName, cancellationToken);
        if (refresh.Instance is not { } checkedInstance)
            return ServiceResult<DokployHealthResultDto>.Failure(refresh.HostError ?? NotDetectedMessage, refresh.HostError is null ? ServiceErrorType.NotFound : ServiceErrorType.Failure);

        return ServiceResult<DokployHealthResultDto>.Success(new DokployHealthResultDto
        {
            Status = checkedInstance.Status,
            Message = checkedInstance.StatusMessage ?? string.Empty,
            CheckedAt = checkedInstance.LastHealthCheckAt ?? UtcNow,
            ResponseTimeMs = checkedInstance.LastResponseTimeMs,
            Version = checkedInstance.Version
        });
    }

    public async Task<ServiceResult<IReadOnlyList<DokployProjectDto>>> GetProjectsAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var instance = await _dokployRepository.GetByServerIdAsync(serverId, cancellationToken);
        if (instance is null)
            return ServiceResult<IReadOnlyList<DokployProjectDto>>.NotFound(NotDetectedMessage);

        var apiKey = TryUnprotect(instance);
        if (apiKey is null)
            return ServiceResult<IReadOnlyList<DokployProjectDto>>.Failure(ApiKeyRequiredMessage, ServiceErrorType.Validation);

        return await _apiClient.GetProjectsAsync(instance.BaseUrl, apiKey, cancellationToken);
    }

    public async Task<ServiceResult> SaveSettingsAsync(Guid serverId, DokploySettingsDto dto, CancellationToken cancellationToken = default)
    {
        dto.BaseUrl = dto.BaseUrl?.Trim() ?? string.Empty;
        dto.ApiKey = TextHelper.NullIfEmpty(dto.ApiKey);
        var validation = await _settingsValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        var server = await _serverRepository.GetByIdAsync(serverId, cancellationToken);
        if (server is null)
            return ServiceResult.NotFound("Sunucu bulunamadı.");

        DokployUrls.TryNormalize(dto.BaseUrl, out var baseUrl);

        if (dto.ApiKey is not null)
        {
            var version = await _apiClient.GetVersionAsync(baseUrl, dto.ApiKey, cancellationToken);
            if (!version.IsSuccess)
            {
                return version.ErrorType == ServiceErrorType.Forbidden
                    ? ServiceResult.ValidationFailure(nameof(DokploySettingsDto.ApiKey), version.Message ?? "API anahtarı doğrulanamadı.")
                    : ServiceResult.ValidationFailure(nameof(DokploySettingsDto.BaseUrl), $"API anahtarı doğrulanamadı: {version.Message}");
            }
        }

        var userName = _currentUser.UserName;
        var instance = await _dokployRepository.GetByServerIdAsync(serverId, cancellationToken);
        if (instance is null)
        {
            instance = new DokployInstance { ServerId = serverId, CreatedBy = userName, CreatedAt = UtcNow };
            await _dokployRepository.AddAsync(instance, cancellationToken);
        }

        var urlChanged = !string.Equals(instance.BaseUrl, baseUrl, StringComparison.Ordinal);
        instance.BaseUrl = baseUrl;
        if (dto.ApiKey is not null)
            instance.EncryptedApiKey = _secretProtector.Protect(dto.ApiKey);
        instance.UpdatedAt = UtcNow;
        instance.UpdatedBy = userName;
        await _dokployRepository.SaveChangesAsync(cancellationToken);

        var changes = new List<string>();
        if (urlChanged)
            changes.Add($"Adres: {baseUrl}");
        if (dto.ApiKey is not null)
            changes.Add("API anahtarı güncellendi");

        await AuditAsync(DokployAuditActions.SettingsUpdate, server, changes.Count > 0 ? string.Join(" | ", changes) : "Değişiklik yok", true, null, cancellationToken);
        return ServiceResult.Success("Dokploy ayarları kaydedildi.");
    }

    public async Task<ServiceResult> RemoveApiKeyAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdAsync(serverId, cancellationToken);
        var instance = await _dokployRepository.GetByServerIdAsync(serverId, cancellationToken);
        if (server is null || instance is null)
            return ServiceResult.NotFound(NotDetectedMessage);

        if (instance.EncryptedApiKey is null)
            return ServiceResult.Success("Kayıtlı API anahtarı yok.");

        instance.EncryptedApiKey = null;
        instance.UpdatedAt = UtcNow;
        instance.UpdatedBy = _currentUser.UserName;
        await _dokployRepository.SaveChangesAsync(cancellationToken);

        await AuditAsync(DokployAuditActions.ApiKeyRemove, server, "API anahtarı kaldırıldı", true, null, cancellationToken);
        return ServiceResult.Success("API anahtarı kaldırıldı.");
    }

    public async Task<ServiceResult<Guid>> BeginInstallationAsync(Guid serverId, DokployInstallRequestDto dto, DokployActor actor, CancellationToken cancellationToken = default)
    {
        dto.Version = TextHelper.NullIfEmpty(dto.Version);
        var validation = await _installValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<Guid>.ValidationFailure(validation);

        var server = await _serverRepository.GetByIdAsync(serverId, cancellationToken);
        if (server is null)
            return ServiceResult<Guid>.NotFound("Sunucu bulunamadı.");

        if (!string.Equals(dto.ConfirmationName?.Trim(), server.Name, StringComparison.Ordinal))
            return ServiceResult<Guid>.ValidationFailure(nameof(DokployInstallRequestDto.ConfirmationName), "Onay için sunucu adını birebir yazın.");

        if (await _dokployRepository.GetRunningInstallationAsync(serverId, cancellationToken) is not null)
            return ServiceResult<Guid>.Failure("Bu sunucuda devam eden bir Dokploy kurulumu var.", ServiceErrorType.Conflict);

        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult<Guid>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        var report = await EvaluateCompatibilityAsync(connection.Data!.Context, cancellationToken);
        if (!report.IsSuccess)
            return ServiceResult<Guid>.Failure(report.Message ?? "Uyumluluk kontrolü yapılamadı.", report.ErrorType);

        if (!report.Data!.CanInstall)
        {
            var failed = report.Data.Checks.First(c => c.Status == DokployCheckStatus.Failed);
            return ServiceResult<Guid>.Failure($"Uyumluluk kontrolü geçilemedi — {failed.Title}: {failed.Detail}", ServiceErrorType.Validation);
        }

        var installation = new DokployInstallation
        {
            ServerId = serverId,
            ServerName = server.Name,
            UserId = actor.UserId,
            UserName = actor.UserName,
            IpAddress = actor.IpAddress,
            RequestedVersion = dto.Version,
            ScriptUrl = _options.InstallScriptUrl,
            Status = DokployInstallationStatus.Running,
            StartedAt = UtcNow
        };
        await _dokployRepository.AddInstallationAsync(installation, cancellationToken);
        await _dokployRepository.SaveChangesAsync(cancellationToken);

        await AuditAsync(DokployAuditActions.InstallStart, server, $"Sürüm: {dto.Version ?? "son kararlı"} | Betik: {_options.InstallScriptUrl}", true, actor, cancellationToken);
        return ServiceResult<Guid>.Success(installation.Id, "Dokploy kurulumu başlatıldı.");
    }

    public async Task<ServiceResult> RunInstallationAsync(Guid installationId, DokployActor actor, IDokployInstallObserver observer, CancellationToken cancellationToken = default)
    {
        var installation = await _dokployRepository.GetInstallationAsync(installationId, cancellationToken);
        if (installation is null || installation.Status != DokployInstallationStatus.Running)
            return ServiceResult.NotFound("Devam eden kurulum bulunamadı.");

        var recorder = new DokployInstallRecorder(
            observer,
            Math.Max(64, _options.MaxStoredOutputKilobytes) * 1024,
            (output, ct) => _dokployRepository.UpdateInstallationOutputAsync(installationId, output, ct),
            _timeProvider);

        try
        {
            var outcome = await InstallAsync(installation, recorder, actor, cancellationToken);
            await CompleteAsync(installation, recorder, outcome, actor, CancellationToken.None);
            return outcome.Succeeded ? ServiceResult.Success(outcome.Message) : ServiceResult.Failure(outcome.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            var outcome = new DokployInstallOutcome(false, InterruptedReason, null, null, DokployInstallationStatus.Interrupted);
            await CompleteAsync(installation, recorder, outcome, actor, CancellationToken.None);
            return ServiceResult.Failure(InterruptedReason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dokploy kurulumu beklenmeyen hata ile bitti. InstallationId: {InstallationId}", installationId);
            var outcome = new DokployInstallOutcome(false, "Beklenmeyen bir hata oluştu; ayrıntılar uygulama loglarında.", null, null);
            await CompleteAsync(installation, recorder, outcome, actor, CancellationToken.None);
            return ServiceResult.Failure(outcome.Message);
        }
    }

    public async Task<Guid?> GetRunningInstallationIdAsync(Guid serverId, CancellationToken cancellationToken = default) =>
        (await _dokployRepository.GetRunningInstallationAsync(serverId, cancellationToken))?.Id;

    public async Task<ServiceResult<DokployInstallationDto>> GetInstallationAsync(Guid serverId, Guid installationId, CancellationToken cancellationToken = default)
    {
        var installation = await _dokployRepository.GetInstallationAsync(installationId, cancellationToken);
        return installation is null || installation.ServerId != serverId
            ? ServiceResult<DokployInstallationDto>.NotFound("Kurulum kaydı bulunamadı.")
            : ServiceResult<DokployInstallationDto>.Success(installation.ToDto(includeOutput: true));
    }

    public Task<int> InterruptRunningInstallationsAsync(CancellationToken cancellationToken = default) =>
        _dokployRepository.InterruptRunningInstallationsAsync(UtcNow, InterruptedReason, cancellationToken);

    public async Task RunScheduledHealthChecksAsync(CancellationToken cancellationToken = default)
    {
        var serverIds = await _dokployRepository.GetServerIdsForHealthCheckAsync(cancellationToken);
        foreach (var serverId in serverIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await RunHealthCheckAsync(serverId, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Dokploy sağlık kontrolü başarısız. ServerId: {ServerId}", serverId);
            }
        }
    }

    private async Task<DokployInstallOutcome> InstallAsync(DokployInstallation installation, DokployInstallRecorder recorder, DokployActor actor, CancellationToken cancellationToken)
    {
        await recorder.OnStageAsync(DokployInstallStage.Checking, "Uyumluluk yeniden kontrol ediliyor", cancellationToken);
        await recorder.InfoAsync($"{installation.ServerName} için Dokploy kurulumu başlıyor.", cancellationToken);

        var server = await _serverRepository.GetByIdAsync(installation.ServerId, cancellationToken);
        if (server is null)
            return new DokployInstallOutcome(false, "Sunucu bulunamadı.", null, null);

        var connection = await _connectionProvider.GetAsync(installation.ServerId, cancellationToken);
        if (!connection.IsSuccess)
            return new DokployInstallOutcome(false, connection.Message ?? "Sunucuya bağlanılamadı.", null, null);

        var context = connection.Data!.Context;
        var report = await EvaluateCompatibilityAsync(context, cancellationToken);
        if (!report.IsSuccess)
            return new DokployInstallOutcome(false, report.Message ?? "Uyumluluk kontrolü yapılamadı.", null, null);

        if (!report.Data!.CanInstall)
        {
            var failed = report.Data.Checks.First(c => c.Status == DokployCheckStatus.Failed);
            return new DokployInstallOutcome(false, $"{failed.Title}: {failed.Detail}", null, null);
        }

        var plan = new DokployInstallPlan(
            installation.ScriptUrl,
            installation.RequestedVersion,
            Elevate: !report.Data.IsRoot,
            UseBash: report.Data.BashAvailable,
            TimeSpan.FromMinutes(Math.Max(5, _options.InstallTimeoutMinutes)),
            string.IsNullOrWhiteSpace(_options.ExpectedSha256) ? null : _options.ExpectedSha256.Trim());

        var script = await _provider.RunInstallScriptAsync(context, plan, recorder, cancellationToken);
        if (!script.IsSuccess)
            return new DokployInstallOutcome(false, script.Message ?? "Kurulum betiği çalıştırılamadı.", null, null);

        var result = script.Data!;
        if (!result.IsSuccess)
        {
            var reason = result.ErrorMessage
                         ?? (result.TimedOut
                             ? $"Kurulum {plan.Timeout.TotalMinutes:0} dakika içinde bitmedi."
                             : $"Kurulum betiği hata ile bitti (çıkış kodu {result.ExitCode?.ToString() ?? "?"}).");
            return new DokployInstallOutcome(false, reason, result.Sha256, result.ExitCode);
        }

        await recorder.OnStageAsync(DokployInstallStage.HealthCheck, "Dokploy'un açılması bekleniyor", cancellationToken);
        var healthy = await WaitUntilHealthyAsync(context, recorder, cancellationToken);

        var instance = await _dokployRepository.GetByServerIdAsync(installation.ServerId, cancellationToken);
        if (instance is null)
        {
            instance = new DokployInstance
            {
                ServerId = installation.ServerId,
                BaseUrl = DokployUrls.BuildDefault(server.IpAddress, _options.Port),
                CreatedBy = actor.UserName,
                CreatedAt = UtcNow
            };
            await _dokployRepository.AddAsync(instance, cancellationToken);
        }

        instance.InstalledAt = UtcNow;
        instance.InstallationId = installation.Id;
        await RefreshAsync(server, instance, actor.UserName, cancellationToken);

        return healthy
            ? new DokployInstallOutcome(true, $"Dokploy kuruldu: {instance.BaseUrl}", result.Sha256, result.ExitCode)
            : new DokployInstallOutcome(false, $"Dokploy kuruldu ancak {_options.StartupTimeoutSeconds} saniye içinde /api/health yanıt vermedi. Durumu Dokploy sekmesinden kontrol edin.", result.Sha256, result.ExitCode);
    }

    private async Task<bool> WaitUntilHealthyAsync(RemoteExecutionContext context, DokployInstallRecorder recorder, CancellationToken cancellationToken)
    {
        var started = _timeProvider.GetUtcNow();
        var deadline = started.AddSeconds(Math.Max(15, _options.StartupTimeoutSeconds));
        var nextProgress = started.Add(HealthProgressInterval);

        while (true)
        {
            if (await _provider.IsLocallyHealthyAsync(context, _options.Port, cancellationToken))
            {
                await recorder.OnOutputAsync(DokployConsole.Success("Dokploy /api/health isteğine yanıt veriyor."), cancellationToken);
                return true;
            }

            var now = _timeProvider.GetUtcNow();
            if (now >= deadline)
                return false;

            if (now >= nextProgress)
            {
                await recorder.InfoAsync($"Dokploy yanıtı bekleniyor… ({(now - started).TotalSeconds:0} sn)", cancellationToken);
                nextProgress = now.Add(HealthProgressInterval);
            }

            await Task.Delay(HealthPollInterval, _timeProvider, cancellationToken);
        }
    }

    private async Task CompleteAsync(DokployInstallation installation, DokployInstallRecorder recorder, DokployInstallOutcome outcome, DokployActor actor, CancellationToken cancellationToken)
    {
        var line = outcome.Succeeded ? DokployConsole.Success(outcome.Message) : DokployConsole.Error(outcome.Message);
        try
        {
            await recorder.OnOutputAsync(line, cancellationToken);
            await recorder.OnStageAsync(outcome.Succeeded ? DokployInstallStage.Completed : DokployInstallStage.Failed, outcome.Message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Kurulum sonucu izleyiciye iletilemedi. InstallationId: {InstallationId}", installation.Id);
        }

        installation.Status = outcome.Status ?? (outcome.Succeeded ? DokployInstallationStatus.Succeeded : DokployInstallationStatus.Failed);
        installation.ScriptSha256 = outcome.Sha256 ?? installation.ScriptSha256;
        installation.ExitCode = outcome.ExitCode;
        installation.FailureReason = outcome.Succeeded ? null : TextHelper.Truncate(outcome.Message, 1000);
        installation.CompletedAt = UtcNow;
        installation.Output = recorder.Log.ToString();
        await _dokployRepository.SaveChangesAsync(cancellationToken);

        var server = await _serverRepository.GetByIdAsync(installation.ServerId, cancellationToken);
        var details = $"Sonuç: {outcome.Message}" + (outcome.Sha256 is null ? string.Empty : $" | Betik SHA-256: {outcome.Sha256}");
        await _auditLogService.LogAsync(new AuditEntry(
            DokployAuditActions.InstallComplete,
            AuditEntityTypes.Server,
            installation.ServerId.ToString(),
            server?.Name ?? installation.ServerName,
            details,
            outcome.Succeeded,
            UserNameOverride: actor.UserName,
            UserIdOverride: actor.UserId,
            IpAddressOverride: actor.IpAddress), cancellationToken);
    }

    private async Task<ServiceResult<DokployCompatibilityReportDto>> EvaluateCompatibilityAsync(RemoteExecutionContext context, CancellationToken cancellationToken)
    {
        var facts = await _provider.GatherFactsAsync(context, _options, cancellationToken);
        return facts.IsSuccess
            ? ServiceResult<DokployCompatibilityReportDto>.Success(DokployCompatibilityEvaluator.Evaluate(facts.Data!, _options, UtcNow))
            : ServiceResult<DokployCompatibilityReportDto>.Failure(facts.Message ?? "Sunucu bilgileri okunamadı.", facts.ErrorType);
    }

    /// <summary>
    /// Sunucudaki durumu (SSH) ve panelden erişimi (HTTP) kontrol edip kaydı günceller.
    /// Kayıt yoksa ve sunucuda Dokploy bulunursa (panel dışında kurulmuş) yeni kayıt açılır.
    /// </summary>
    private async Task<DokployRefreshResult> RefreshAsync(Server server, DokployInstance? instance, string? userName, CancellationToken cancellationToken)
    {
        DokployHostStatusDto? host = null;
        string? hostError;
        var connection = await _connectionProvider.GetAsync(server.Id, cancellationToken);
        if (connection.IsSuccess)
        {
            var status = await _provider.GetStatusAsync(connection.Data!.Context, _options.Port, cancellationToken);
            host = status.Data;
            hostError = status.IsSuccess ? null : status.Message;
        }
        else
        {
            hostError = connection.Message;
        }

        var detected = false;
        if (instance is null)
        {
            if (host?.IsInstalled != true)
                return new DokployRefreshResult(null, host, hostError);

            instance = new DokployInstance
            {
                ServerId = server.Id,
                BaseUrl = DokployUrls.BuildDefault(server.IpAddress, _options.Port),
                CreatedBy = userName,
                CreatedAt = UtcNow
            };
            await _dokployRepository.AddAsync(instance, cancellationToken);
            detected = true;
        }

        var probe = await _apiClient.ProbeHealthAsync(instance.BaseUrl, cancellationToken);
        var (evaluatedStatus, message) = DokployStatusEvaluator.Evaluate(host, hostError, probe);

        var version = host?.ImageTag;
        var apiKey = probe.IsSuccess ? TryUnprotect(instance) : null;
        if (apiKey is not null)
        {
            var apiVersion = await _apiClient.GetVersionAsync(instance.BaseUrl, apiKey, cancellationToken);
            if (apiVersion.IsSuccess && !string.IsNullOrWhiteSpace(apiVersion.Data))
                version = apiVersion.Data;
        }

        instance.Status = evaluatedStatus;
        instance.StatusMessage = TextHelper.Truncate(message, 1000);
        instance.LastHealthCheckAt = UtcNow;
        instance.LastResponseTimeMs = probe.ResponseTimeMs;
        if (version is not null && (version != "latest" || instance.Version is null))
            instance.Version = TextHelper.Truncate(version, 100);
        instance.UpdatedAt = UtcNow;
        await _dokployRepository.SaveChangesAsync(cancellationToken);

        if (detected)
            await AuditAsync(DokployAuditActions.Detected, server, $"Adres: {instance.BaseUrl} | Durum: {evaluatedStatus}", true, null, cancellationToken);

        return new DokployRefreshResult(instance, host, hostError);
    }

    private string? TryUnprotect(DokployInstance instance)
    {
        if (string.IsNullOrEmpty(instance.EncryptedApiKey))
            return null;

        try
        {
            return _secretProtector.Unprotect(instance.EncryptedApiKey);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Dokploy API anahtarı çözülemedi. ServerId: {ServerId}", instance.ServerId);
            return null;
        }
    }

    private Task AuditAsync(string action, Server server, string details, bool isSuccess, DokployActor? actor, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(
            action,
            AuditEntityTypes.Server,
            server.Id.ToString(),
            server.Name,
            details,
            isSuccess,
            UserNameOverride: actor?.UserName,
            UserIdOverride: actor?.UserId,
            IpAddressOverride: actor?.IpAddress), cancellationToken);
}
