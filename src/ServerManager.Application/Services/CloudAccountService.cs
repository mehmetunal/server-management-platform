using FluentValidation;
using Microsoft.Extensions.Logging;
using ServerManager.Application.Auditing;
using ServerManager.Application.Cloud;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Cloud;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Cloud;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Entities;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Services;

public class CloudAccountService : ICloudAccountService
{
    private const string NotFoundMessage = "Bulut hesabı bulunamadı.";
    private const string ProviderDisabledMessage = "Bu hesabın sağlayıcı eklentisi kurulu veya etkin değil. Eklentiler sayfasından etkinleştirin.";

    private readonly ICloudAccountRepository _repository;
    private readonly IServerTemplateRepository _templateRepository;
    private readonly ICloudProviderRegistry _providers;
    private readonly ISecretProtector _protector;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CloudAccountFormDto> _formValidator;
    private readonly IValidator<CloudProvisionDto> _provisionValidator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CloudAccountService> _logger;

    public CloudAccountService(
        ICloudAccountRepository repository,
        IServerTemplateRepository templateRepository,
        ICloudProviderRegistry providers,
        ISecretProtector protector,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IValidator<CloudAccountFormDto> formValidator,
        IValidator<CloudProvisionDto> provisionValidator,
        TimeProvider timeProvider,
        ILogger<CloudAccountService> logger)
    {
        _repository = repository;
        _templateRepository = templateRepository;
        _providers = providers;
        _protector = protector;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _formValidator = formValidator;
        _provisionValidator = provisionValidator;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    public IReadOnlyList<CloudProviderOptionDto> GetProviders() =>
        _providers.GetEnabled().Select(p => new CloudProviderOptionDto(p.SystemName, p.DisplayName, p.TokenHelp)).ToList();

    public async Task<IReadOnlyList<CloudAccountListItemDto>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await _repository.GetAllWithServersAsync(cancellationToken);
        return accounts.Select(ToListItem).ToList();
    }

    public async Task<IReadOnlyList<CloudAccountOptionDto>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await _repository.GetAllWithServersAsync(cancellationToken);
        return accounts
            .Where(a => _providers.Find(a.Provider) is not null)
            .Select(a => new CloudAccountOptionDto(a.Id, a.Name, ProviderName(a.Provider)))
            .ToList();
    }

    public async Task<ServiceResult<CloudAccountFormDto>> GetForEditAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await _repository.GetAsync(id, cancellationToken);
        return account is null
            ? ServiceResult<CloudAccountFormDto>.NotFound(NotFoundMessage)
            : ServiceResult<CloudAccountFormDto>.Success(new CloudAccountFormDto { Id = account.Id, Name = account.Name, Provider = account.Provider });
    }

    public async Task<ServiceResult<Guid>> CreateAsync(CloudAccountFormDto dto, CancellationToken cancellationToken = default)
    {
        dto.Id = null;
        Normalize(dto);
        var validation = await _formValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<Guid>.ValidationFailure(validation);

        var provider = _providers.Find(dto.Provider);
        if (provider is null)
            return ServiceResult<Guid>.ValidationFailure(nameof(dto.Provider), "Seçilen sağlayıcı kullanılamıyor.");

        if (await _repository.NameExistsAsync(dto.Name, null, cancellationToken))
            return ServiceResult<Guid>.ValidationFailure(nameof(dto.Name), "Bu isimde bir hesap zaten var.");

        var check = await provider.ValidateTokenAsync(dto.Token!, cancellationToken);
        if (!check.IsSuccess)
            return ServiceResult<Guid>.ValidationFailure(nameof(dto.Token), check.Message ?? "API anahtarı doğrulanamadı.");

        var account = new CloudAccount
        {
            Name = dto.Name,
            Provider = provider.SystemName,
            EncryptedToken = _protector.Protect(dto.Token!),
            AccountLabel = TextHelper.Truncate(check.Data, 200),
            CreatedAt = UtcNow,
            CreatedBy = _currentUser.UserName
        };
        await _repository.AddAsync(account, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.CloudAccountCreate, account, $"Sağlayıcı: {provider.DisplayName}", cancellationToken);
        return ServiceResult<Guid>.Success(account.Id, "Hesap eklendi ve API anahtarı doğrulandı.");
    }

    public async Task<ServiceResult> UpdateAsync(CloudAccountFormDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Id is not { } id)
            return ServiceResult.NotFound(NotFoundMessage);

        var account = await _repository.GetAsync(id, cancellationToken);
        if (account is null)
            return ServiceResult.NotFound(NotFoundMessage);

        dto.Provider = account.Provider;
        Normalize(dto);
        var validation = await _formValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        if (await _repository.NameExistsAsync(dto.Name, id, cancellationToken))
            return ServiceResult.ValidationFailure(nameof(dto.Name), "Bu isimde bir hesap zaten var.");

        var changes = new List<string>();
        if (account.Name != dto.Name) changes.Add($"Ad: {account.Name} -> {dto.Name}");

        if (dto.Token is not null)
        {
            var provider = _providers.Find(account.Provider);
            if (provider is null)
                return ServiceResult.Failure(ProviderDisabledMessage);

            var check = await provider.ValidateTokenAsync(dto.Token, cancellationToken);
            if (!check.IsSuccess)
                return ServiceResult.ValidationFailure(nameof(dto.Token), check.Message ?? "API anahtarı doğrulanamadı.");

            account.EncryptedToken = _protector.Protect(dto.Token);
            account.AccountLabel = TextHelper.Truncate(check.Data, 200);
            account.LastSyncError = null;
            changes.Add("API anahtarı yenilendi");
        }

        account.Name = dto.Name;
        account.UpdatedAt = UtcNow;
        account.UpdatedBy = _currentUser.UserName;
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.CloudAccountUpdate, account, changes.Count == 0 ? "Değişiklik yok" : string.Join(" | ", changes), cancellationToken);
        return ServiceResult.Success("Hesap güncellendi.");
    }

    public async Task<ServiceResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await _repository.GetWithServersAsync(id, cancellationToken);
        if (account is null)
            return ServiceResult.NotFound(NotFoundMessage);

        var linked = account.Servers.ToList();
        foreach (var server in linked)
        {
            server.CloudAccountId = null;
            server.CloudExternalId = null;
        }

        account.IsDeleted = true;
        account.DeletedAt = UtcNow;
        account.DeletedBy = _currentUser.UserName;
        await _repository.SaveChangesAsync(cancellationToken);

        await AuditAsync(AuditActions.CloudAccountDelete, account, linked.Count == 0 ? null : $"Bağlantısı kaldırılan sunucu sayısı: {linked.Count}", cancellationToken);
        return ServiceResult.Success("Hesap silindi. Sağlayıcıdaki sunuculara ve paneldeki sunucu kayıtlarına dokunulmadı.");
    }

    public async Task<ServiceResult<CloudAccountServersDto>> GetServersAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await _repository.GetWithServersAsync(id, cancellationToken);
        if (account is null)
            return ServiceResult<CloudAccountServersDto>.NotFound(NotFoundMessage);

        var provider = _providers.Find(account.Provider);
        if (provider is null)
            return ServiceResult<CloudAccountServersDto>.Failure(ProviderDisabledMessage);

        var list = await provider.ListServersAsync(_protector.Unprotect(account.EncryptedToken), cancellationToken);
        if (!list.IsSuccess || list.Data is null)
            return ServiceResult<CloudAccountServersDto>.Failure(list.Message ?? "Sunucu listesi alınamadı.");

        var linked = account.Servers
            .Where(s => s.CloudExternalId is not null)
            .GroupBy(s => s.CloudExternalId!)
            .ToDictionary(g => g.Key, g => g.First());

        var servers = list.Data
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => linked.TryGetValue(s.ExternalId, out var server)
                ? new CloudServerListItemDto(s, server.Id, server.Name)
                : new CloudServerListItemDto(s, null, null))
            .ToList();

        return ServiceResult<CloudAccountServersDto>.Success(new CloudAccountServersDto(ToListItem(account), servers));
    }

    public async Task<ServiceResult<CloudSyncSummary>> SyncAsync(Guid id, bool automatic = false, CancellationToken cancellationToken = default)
    {
        var account = await _repository.GetWithServersAsync(id, cancellationToken);
        if (account is null)
            return ServiceResult<CloudSyncSummary>.NotFound(NotFoundMessage);

        var provider = _providers.Find(account.Provider);
        if (provider is null)
            return ServiceResult<CloudSyncSummary>.Failure(ProviderDisabledMessage);

        var list = await provider.ListServersAsync(_protector.Unprotect(account.EncryptedToken), cancellationToken);
        account.LastSyncAt = UtcNow;
        if (!list.IsSuccess || list.Data is null)
        {
            account.LastSyncError = TextHelper.Truncate(list.Message ?? "Sunucu listesi alınamadı.", 1000);
            await _repository.SaveChangesAsync(cancellationToken);
            if (!automatic)
                await AuditAsync(AuditActions.CloudSync, account, $"Başarısız: {account.LastSyncError}", cancellationToken, isSuccess: false);
            return ServiceResult<CloudSyncSummary>.Failure(account.LastSyncError!);
        }

        account.LastSyncError = null;
        var remote = list.Data.ToDictionary(s => s.ExternalId);
        var changes = new List<string>();
        var newlyLinked = 0;
        var costUpdated = 0;

        var unlinked = await _repository.GetUnlinkedServersAsync(cancellationToken);
        var linkedIds = account.Servers.Where(s => s.CloudExternalId is not null).Select(s => s.CloudExternalId!).ToHashSet();
        foreach (var info in list.Data.Where(s => !linkedIds.Contains(s.ExternalId) && !string.IsNullOrEmpty(s.PublicIpv4)))
        {
            var matches = unlinked.Where(s => s.IpAddress == info.PublicIpv4).ToList();
            if (matches.Count != 1)
                continue;

            var server = matches[0];
            server.CloudAccountId = account.Id;
            server.CloudExternalId = info.ExternalId;
            account.Servers.Add(server);
            unlinked = unlinked.Where(s => s.Id != server.Id).ToList();
            newlyLinked++;
            changes.Add($"Bağlandı: {server.Name} ({info.Name})");
        }

        foreach (var server in account.Servers.Where(s => s.CloudExternalId is not null))
        {
            if (!remote.TryGetValue(server.CloudExternalId!, out var info) || info.MonthlyPrice is not { } price)
                continue;

            var currency = info.Currency ?? provider.Currency;
            if (server.MonthlyCost == price && server.CostCurrency == currency)
                continue;

            changes.Add($"{server.Name}: {server.MonthlyCost?.ToString("0.00") ?? "—"} {server.CostCurrency} -> {price:0.00} {currency}");
            server.MonthlyCost = price;
            server.CostCurrency = currency;
            costUpdated++;
        }

        await _repository.SaveChangesAsync(cancellationToken);

        var linkedCount = account.Servers.Count(s => s.CloudExternalId is not null && remote.ContainsKey(s.CloudExternalId));
        var summary = new CloudSyncSummary(list.Data.Count, linkedCount, newlyLinked, costUpdated);
        if (!automatic || changes.Count > 0)
            await AuditAsync(AuditActions.CloudSync, account,
                $"Sağlayıcıdaki sunucu: {summary.ProviderServerCount}, bağlı: {linkedCount}" + (changes.Count == 0 ? string.Empty : " | " + string.Join(" | ", changes)),
                cancellationToken);

        var message = $"{summary.ProviderServerCount} sunucu okundu; {linkedCount} tanesi panele bağlı."
            + (newlyLinked > 0 ? $" IP adresi eşleşen {newlyLinked} sunucu hesaba bağlandı." : string.Empty)
            + (costUpdated > 0 ? $" {costUpdated} sunucunun aylık maliyeti güncellendi." : string.Empty);
        return ServiceResult<CloudSyncSummary>.Success(summary, message);
    }

    public async Task<int> SyncAllAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await _repository.GetAllWithServersAsync(cancellationToken);
        var synced = 0;
        foreach (var account in accounts.Where(a => _providers.Find(a.Provider) is not null))
        {
            try
            {
                var result = await SyncAsync(account.Id, automatic: true, cancellationToken);
                if (result.IsSuccess)
                    synced++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Bulut hesabı eşitlenemedi. AccountId: {AccountId}", account.Id);
            }
        }

        return synced;
    }

    public async Task<ServiceResult<CloudCatalog>> GetCatalogAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await _repository.GetAsync(id, cancellationToken);
        if (account is null)
            return ServiceResult<CloudCatalog>.NotFound(NotFoundMessage);

        var provider = _providers.Find(account.Provider);
        return provider is null
            ? ServiceResult<CloudCatalog>.Failure(ProviderDisabledMessage)
            : await provider.GetCatalogAsync(_protector.Unprotect(account.EncryptedToken), cancellationToken);
    }

    public async Task<ServiceResult<CloudProvisionResultDto>> ProvisionAsync(CloudProvisionDto dto, CancellationToken cancellationToken = default)
    {
        dto.Name = dto.Name?.Trim() ?? string.Empty;
        dto.SshPublicKey = TextHelper.NullIfEmpty(dto.SshPublicKey?.Trim());
        var validation = await _provisionValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<CloudProvisionResultDto>.ValidationFailure(validation);

        var account = await _repository.GetAsync(dto.AccountId, cancellationToken);
        if (account is null)
            return ServiceResult<CloudProvisionResultDto>.ValidationFailure(nameof(dto.AccountId), NotFoundMessage);

        var provider = _providers.Find(account.Provider);
        if (provider is null)
            return ServiceResult<CloudProvisionResultDto>.Failure(ProviderDisabledMessage);

        ServerTemplate? template = null;
        if (dto.TemplateId is { } templateId)
        {
            template = await _templateRepository.GetAsync(templateId, cancellationToken);
            if (template is null || template.Kind != ServerTemplateKind.CloudInit)
                return ServiceResult<CloudProvisionResultDto>.ValidationFailure(nameof(dto.TemplateId), "Seçilen cloud-init şablonu bulunamadı.");
        }

        var userData = CloudInitBuilder.Build(template?.Content, dto.SshPublicKey);
        if (userData is not null && System.Text.Encoding.UTF8.GetByteCount(userData) > CloudInitBuilder.MaxUserDataBytes)
            return ServiceResult<CloudProvisionResultDto>.ValidationFailure(nameof(dto.TemplateId), "cloud-init içeriği 32 KB sınırını aşıyor.");

        var request = new CloudCreateServerRequest(dto.Name, dto.Region, dto.Size, dto.Image, userData);
        var result = await provider.CreateServerAsync(_protector.Unprotect(account.EncryptedToken), request, cancellationToken);

        var details = $"Ad: {dto.Name} | Bölge: {dto.Region} | Tip: {dto.Size} | İmaj: {dto.Image}"
            + (template is null ? string.Empty : $" | Şablon: {template.Name}")
            + (dto.SshPublicKey is null ? string.Empty : " | SSH anahtarı eklendi");
        if (!result.IsSuccess || result.Data is null)
        {
            await AuditAsync(AuditActions.CloudProvision, account, $"{details} | Hata: {result.Message}", cancellationToken, isSuccess: false);
            return ServiceResult<CloudProvisionResultDto>.Failure(result.Message ?? "Sunucu oluşturulamadı.");
        }

        await AuditAsync(AuditActions.CloudProvision, account, $"{details} | Kimlik: {result.Data.Server.ExternalId}", cancellationToken);
        return ServiceResult<CloudProvisionResultDto>.Success(
            new CloudProvisionResultDto(account.Id, result.Data.Server, result.Data.RootPassword),
            $"{dto.Name} oluşturuldu. Açılması birkaç dakika sürebilir; ardından listeden panele ekleyin.");
    }

    private CloudAccountListItemDto ToListItem(CloudAccount account)
    {
        var costs = account.Servers
            .Where(s => s.MonthlyCost.HasValue)
            .GroupBy(s => s.CostCurrency ?? CostCurrencies.Default)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.MonthlyCost!.Value));
        return new CloudAccountListItemDto(
            account.Id,
            account.Name,
            account.Provider,
            ProviderName(account.Provider),
            _providers.Find(account.Provider) is not null,
            account.AccountLabel,
            account.LastSyncAt,
            account.LastSyncError,
            account.Servers.Count,
            costs);
    }

    private string ProviderName(string systemName) => _providers.FindDisplayName(systemName) ?? systemName;

    private static void Normalize(CloudAccountFormDto dto)
    {
        dto.Name = dto.Name?.Trim() ?? string.Empty;
        dto.Provider = dto.Provider?.Trim() ?? string.Empty;
        dto.Token = TextHelper.NullIfEmpty(dto.Token?.Trim());
    }

    private Task AuditAsync(string action, CloudAccount account, string? details, CancellationToken cancellationToken, bool isSuccess = true) =>
        _auditLogService.LogAsync(
            new AuditEntry(action, AuditEntityTypes.CloudAccount, account.Id.ToString(), account.Name, TextHelper.Truncate(details, 2000), isSuccess),
            cancellationToken);
}
