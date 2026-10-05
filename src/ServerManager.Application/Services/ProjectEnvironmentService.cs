using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Services;

public class ProjectEnvironmentService : IProjectEnvironmentService
{
    private const string NotFoundMessage = "Proje bulunamadı.";
    private const string UnreadableMessage =
        "Kayıtlı ortam değişkenleri çözülemedi (şifreleme anahtarı değişmiş olabilir). İçe aktarmada \"Tümünü değiştir\" ile yeniden girin.";
    private const int MaxAuditKeys = 30;

    /// <summary>Aynı projede eşzamanlı iki düzenleme birbirinin değişikliğini ezmesin diye oku-değiştir-yaz bölümü sıralanır.</summary>
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> ProjectLocks = new();

    private readonly IDeploymentRepository _repository;
    private readonly ISecretProtector _secretProtector;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProjectEnvironmentService> _logger;

    public ProjectEnvironmentService(
        IDeploymentRepository repository,
        ISecretProtector secretProtector,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        TimeProvider timeProvider,
        ILogger<ProjectEnvironmentService> logger)
    {
        _repository = repository;
        _secretProtector = secretProtector;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<ServiceResult<ProjectEnvironmentDto>> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetProjectAsync(projectId, cancellationToken);
        if (project is null)
            return ServiceResult<ProjectEnvironmentDto>.NotFound(NotFoundMessage);

        var document = Read(project);
        return ServiceResult<ProjectEnvironmentDto>.Success(new ProjectEnvironmentDto
        {
            ProjectId = project.Id,
            BuildType = project.BuildType,
            Keys = document?.Keys ?? [],
            HasEnvironment = project.EncryptedEnvironment is not null,
            Unreadable = project.EncryptedEnvironment is not null && document is null
        });
    }

    public async Task<ServiceResult<IReadOnlyList<string>>> GetEnvironmentKeysAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetProjectAsync(projectId, cancellationToken);
        if (project is null)
            return ServiceResult<IReadOnlyList<string>>.NotFound(NotFoundMessage);

        var document = Read(project);
        return document is null
            ? ServiceResult<IReadOnlyList<string>>.Failure(UnreadableMessage)
            : ServiceResult<IReadOnlyList<string>>.Success(document.Keys);
    }

    public async Task<ServiceResult<string>> RevealValueAsync(Guid projectId, string key, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetProjectAsync(projectId, cancellationToken);
        if (project is null)
            return ServiceResult<string>.NotFound(NotFoundMessage);

        var document = Read(project);
        if (document is null)
            return ServiceResult<string>.Failure(UnreadableMessage);

        var value = document.GetValue(key ?? string.Empty);
        if (value is null)
            return ServiceResult<string>.NotFound($"{key} değişkeni bulunamadı.");

        await AuditAsync(AuditActions.ProjectEnvironmentReveal, project, $"Anahtar: {key}", cancellationToken);
        return ServiceResult<string>.Success(value);
    }

    public Task<ServiceResult> SetVariableAsync(Guid projectId, EnvironmentVariableDto dto, CancellationToken cancellationToken = default)
    {
        var key = dto.Key?.Trim() ?? string.Empty;
        if (!EnvironmentDocument.IsValidKey(key))
            return Task.FromResult(ServiceResult.ValidationFailure(nameof(dto.Key), "Anahtar harf veya alt çizgiyle başlar; harf, rakam ve alt çizgi içerir."));

        var value = dto.Value ?? string.Empty;
        if (!EnvironmentDocument.TryValidateValue(value, out var valueError))
            return Task.FromResult(ServiceResult.ValidationFailure(nameof(dto.Value), valueError!));

        return EditAsync(projectId, document =>
        {
            var exists = document.ContainsKey(key);
            if (dto.IsNew && exists)
                return (ServiceResult.ValidationFailure(nameof(dto.Key), $"{key} zaten tanımlı; değerini listeden değiştirin."), null);

            if (!dto.IsNew && !exists)
                return (ServiceResult.NotFound($"{key} değişkeni bulunamadı."), null);

            document.Set(key, value);
            var change = dto.IsNew
                ? new EnvironmentChangeResultDto { Added = [key] }
                : new EnvironmentChangeResultDto { Updated = [key] };
            return (ServiceResult.Success(dto.IsNew ? $"{key} eklendi." : $"{key} güncellendi."), change);
        }, cancellationToken);
    }

    public Task<ServiceResult> DeleteVariableAsync(Guid projectId, string key, CancellationToken cancellationToken = default) =>
        EditAsync(projectId, document =>
        {
            if (!document.Remove(key ?? string.Empty))
                return (ServiceResult.NotFound($"{key} değişkeni bulunamadı."), null);

            return (ServiceResult.Success($"{key} silindi."), new EnvironmentChangeResultDto { Removed = [key!] });
        }, cancellationToken);

    public async Task<ServiceResult<EnvironmentChangeResultDto>> ImportAsync(
        Guid projectId, string? content, EnvironmentImportMode mode, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mode))
            return ServiceResult<EnvironmentChangeResultDto>.ValidationFailure("Mode", "Geçersiz içe aktarma seçeneği.");

        if (string.IsNullOrWhiteSpace(content) && mode != EnvironmentImportMode.Replace)
            return ServiceResult<EnvironmentChangeResultDto>.ValidationFailure("Content", "İçe aktarılacak .env içeriğini yapıştırın veya dosya seçin.");

        if (!EnvironmentDocument.TryParse(content, out var incoming, out var error))
            return ServiceResult<EnvironmentChangeResultDto>.ValidationFailure("Content", error!);

        var trailing = incoming.Entries().FirstOrDefault(entry => !EnvironmentDocument.TryValidateValue(entry.Value, out _));
        if (trailing.Key is not null)
            return ServiceResult<EnvironmentChangeResultDto>.ValidationFailure("Content", $"{trailing.Key} değeri geçersiz.");

        if (mode == EnvironmentImportMode.Replace)
            return await ReplaceAsync(projectId, incoming, cancellationToken);

        return await MergeAsync(projectId, incoming.Entries(), mode == EnvironmentImportMode.Overwrite, "İçe aktarma", cancellationToken);
    }

    public async Task<ServiceResult<string>> ExportAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await _repository.GetProjectAsync(projectId, cancellationToken);
        if (project is null)
            return ServiceResult<string>.NotFound(NotFoundMessage);

        var document = Read(project);
        if (document is null)
            return ServiceResult<string>.Failure(UnreadableMessage);

        await AuditAsync(AuditActions.ProjectEnvironmentExport, project, $"{document.Count} değişken indirildi", cancellationToken);
        return ServiceResult<string>.Success(document.ToString());
    }

    public async Task<ServiceResult<EnvironmentChangeResultDto>> UpsertEnvironmentVariablesAsync(
        Guid projectId,
        IReadOnlyDictionary<string, string> vars,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vars);
        foreach (var (key, value) in vars)
        {
            if (!EnvironmentDocument.IsValidKey(key))
                return ServiceResult<EnvironmentChangeResultDto>.ValidationFailure(key ?? string.Empty, $"Geçersiz anahtar: {key}");

            if (!EnvironmentDocument.TryValidateValue(value, out var valueError))
                return ServiceResult<EnvironmentChangeResultDto>.ValidationFailure(key, $"{key}: {valueError}");
        }

        return await MergeAsync(projectId, vars, overwrite, "Programatik güncelleme", cancellationToken);
    }

    public async Task<ServiceResult<EnvironmentChangeResultDto>> RemoveEnvironmentVariablesAsync(
        Guid projectId,
        IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);
        EnvironmentChangeResultDto? summary = null;
        var result = await EditAsync(projectId, document =>
        {
            var removed = keys.Distinct(StringComparer.Ordinal).Where(document.Remove).ToList();
            summary = new EnvironmentChangeResultDto { Removed = removed };
            return (ServiceResult.Success(Describe(summary)), summary);
        }, cancellationToken, "Servis bağı kaldırıldı");

        return result.IsSuccess
            ? ServiceResult<EnvironmentChangeResultDto>.Success(summary!, result.Message)
            : ServiceResult<EnvironmentChangeResultDto>.Failure(result.Message ?? "Ortam değişkenleri silinemedi.", result.ErrorType);
    }

    private async Task<ServiceResult<EnvironmentChangeResultDto>> MergeAsync(
        Guid projectId,
        IEnumerable<KeyValuePair<string, string>> entries,
        bool overwrite,
        string source,
        CancellationToken cancellationToken)
    {
        EnvironmentChangeResultDto? summary = null;
        var result = await EditAsync(projectId, document =>
        {
            var merged = document.Merge(entries, overwrite);
            summary = new EnvironmentChangeResultDto { Added = merged.Added, Updated = merged.Updated, Skipped = merged.Skipped };
            return (ServiceResult.Success(Describe(summary)), summary);
        }, cancellationToken, source);

        return result.IsSuccess
            ? ServiceResult<EnvironmentChangeResultDto>.Success(summary!, result.Message)
            : ServiceResult<EnvironmentChangeResultDto>.Failure(result.Message ?? "Ortam değişkenleri kaydedilemedi.", result.ErrorType);
    }

    private async Task<ServiceResult<EnvironmentChangeResultDto>> ReplaceAsync(Guid projectId, EnvironmentDocument incoming, CancellationToken cancellationToken)
    {
        var gate = ProjectLocks.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var project = await _repository.GetProjectAsync(projectId, cancellationToken);
            if (project is null)
                return ServiceResult<EnvironmentChangeResultDto>.NotFound(NotFoundMessage);

            var previous = Read(project);
            var oldKeys = previous?.Keys ?? [];
            var newKeys = incoming.Keys;
            var summary = new EnvironmentChangeResultDto
            {
                Added = newKeys.Except(oldKeys, StringComparer.Ordinal).ToList(),
                Updated = newKeys.Intersect(oldKeys, StringComparer.Ordinal)
                    .Where(key => !string.Equals(previous!.GetValue(key), incoming.GetValue(key), StringComparison.Ordinal))
                    .ToList(),
                Removed = oldKeys.Except(newKeys, StringComparer.Ordinal).ToList()
            };

            await SaveAsync(project, incoming, cancellationToken);
            var details = "Tümü değiştirildi" + (previous is null && project.EncryptedEnvironment is not null ? " (önceki kayıt çözülemiyordu)" : string.Empty) + " | " + Describe(summary);
            await AuditAsync(AuditActions.ProjectEnvironmentUpdate, project, details, cancellationToken);
            return ServiceResult<EnvironmentChangeResultDto>.Success(summary, Describe(summary));
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Kaydı kilit altında çözer, <paramref name="edit"/> ile değiştirir, sınırları doğrular ve şifreleyip kaydeder.</summary>
    private async Task<ServiceResult> EditAsync(
        Guid projectId,
        Func<EnvironmentDocument, (ServiceResult Result, EnvironmentChangeResultDto? Change)> edit,
        CancellationToken cancellationToken,
        string? source = null)
    {
        var gate = ProjectLocks.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var project = await _repository.GetProjectAsync(projectId, cancellationToken);
            if (project is null)
                return ServiceResult.NotFound(NotFoundMessage);

            var document = project.EncryptedEnvironment is null ? EnvironmentDocument.Empty() : Read(project);
            if (document is null)
                return ServiceResult.Failure(UnreadableMessage);

            var (result, change) = edit(document);
            if (!result.IsSuccess || change is null || !change.HasChanges)
                return result;

            var text = document.ToString();
            if (!EnvironmentFile.TryParse(text, out _, out var limitError))
                return ServiceResult.ValidationFailure("Content", limitError!);

            await SaveAsync(project, document, cancellationToken);
            var details = (source is null ? string.Empty : source + " | ") + Describe(change);
            await AuditAsync(AuditActions.ProjectEnvironmentUpdate, project, details, cancellationToken);
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task SaveAsync(DeploymentProject project, EnvironmentDocument document, CancellationToken cancellationToken)
    {
        // Tüm değişkenler silinse de kayıt (boş içerik) kalır: sonraki deploy sunucudaki .env'yi de boşaltır.
        project.EncryptedEnvironment = _secretProtector.Protect(document.ToString());
        project.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        project.UpdatedBy = _currentUser.UserName;
        await _repository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Proje ortam değişkenleri güncellendi. ProjectId: {ProjectId}, Count: {Count}", project.Id, document.Count);
    }

    private EnvironmentDocument? Read(DeploymentProject project)
    {
        if (project.EncryptedEnvironment is null)
            return EnvironmentDocument.Empty();

        try
        {
            var text = _secretProtector.Unprotect(project.EncryptedEnvironment);
            if (EnvironmentDocument.TryParse(text, out var document, out var error))
                return document;

            _logger.LogWarning("Kayıtlı ortam değişkenleri ayrıştırılamadı. ProjectId: {ProjectId}, Error: {Error}", project.Id, error);
            return null;
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Proje ortam değişkenleri çözülemedi. ProjectId: {ProjectId}", project.Id);
            return null;
        }
    }

    private static string Describe(EnvironmentChangeResultDto change)
    {
        var parts = new List<string>();
        if (change.Added.Count > 0)
            parts.Add($"Eklenen: {Keys(change.Added)}");
        if (change.Updated.Count > 0)
            parts.Add($"Değişen: {Keys(change.Updated)}");
        if (change.Removed.Count > 0)
            parts.Add($"Silinen: {Keys(change.Removed)}");
        if (change.Skipped.Count > 0)
            parts.Add($"Korunan (zaten var): {Keys(change.Skipped)}");
        return parts.Count == 0 ? "Değişiklik yok." : string.Join(" | ", parts);
    }

    private static string Keys(IReadOnlyList<string> keys) =>
        keys.Count <= MaxAuditKeys ? string.Join(", ", keys) : string.Join(", ", keys.Take(MaxAuditKeys)) + $" (+{keys.Count - MaxAuditKeys})";

    private Task AuditAsync(string action, DeploymentProject project, string details, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(
            action,
            AuditEntityTypes.Project,
            project.Id.ToString(),
            project.Name,
            TextHelper.Truncate(details, 2000)), cancellationToken);
}
