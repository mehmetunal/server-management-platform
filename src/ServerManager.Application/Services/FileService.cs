using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Auditing;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Files;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Files;
using ServerManager.Application.Interfaces.Files;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Interfaces.Ssh;

namespace ServerManager.Application.Services;

public class FileService : IFileService
{
    private const int AccountFileMaxBytes = 2 * 1024 * 1024;
    private const string InvalidPathMessage = "Geçerli bir mutlak yol girin (ör. /var/www).";
    private const string ProtectedPathMessage = "Bu yol korumalı; panelden silinemez, taşınamaz ve izinleri değiştirilemez.";
    private const string ProtectedTreeMessage = "Bu klasör sistem klasörü veya korumalı bir klasörü içeriyor; alt öğeleriyle birlikte silinemez ve izinleri değiştirilemez.";
    private const string ExistsMessage = "Hedefte aynı adla bir dosya veya klasör zaten var.";

    private readonly IServerConnectionProvider _connectionProvider;
    private readonly IRemoteFileSystem _fileSystem;
    private readonly IAuditLogService _auditLogService;
    private readonly IValidator<SaveFileDto> _saveValidator;
    private readonly IValidator<CreateFileEntryDto> _createValidator;
    private readonly IValidator<MoveFileDto> _moveValidator;
    private readonly IValidator<CopyFileDto> _copyValidator;
    private readonly IValidator<ChangePermissionsDto> _permissionsValidator;
    private readonly IValidator<UploadFileDto> _uploadValidator;
    private readonly FileManagerOptions _options;
    private readonly ILogger<FileService> _logger;

    public FileService(
        IServerConnectionProvider connectionProvider,
        IRemoteFileSystem fileSystem,
        IAuditLogService auditLogService,
        IValidator<SaveFileDto> saveValidator,
        IValidator<CreateFileEntryDto> createValidator,
        IValidator<MoveFileDto> moveValidator,
        IValidator<CopyFileDto> copyValidator,
        IValidator<ChangePermissionsDto> permissionsValidator,
        IValidator<UploadFileDto> uploadValidator,
        IOptions<FileManagerOptions> options,
        ILogger<FileService> logger)
    {
        _connectionProvider = connectionProvider;
        _fileSystem = fileSystem;
        _auditLogService = auditLogService;
        _saveValidator = saveValidator;
        _createValidator = createValidator;
        _moveValidator = moveValidator;
        _copyValidator = copyValidator;
        _permissionsValidator = permissionsValidator;
        _uploadValidator = uploadValidator;
        _options = options.Value;
        _logger = logger;
    }

    private int MaxEditBytes => Math.Max(1, _options.MaxEditKilobytes) * 1024;

    private long MaxUploadBytes => Math.Max(1, _options.MaxUploadMegabytes) * 1024L * 1024L;

    public async Task<ServiceResult<FileListingDto>> ListAsync(Guid serverId, string? path, CancellationToken cancellationToken = default)
    {
        string? requested = null;
        if (!string.IsNullOrWhiteSpace(path))
        {
            requested = RemotePath.Normalize(path);
            if (requested is null)
                return ServiceResult<FileListingDto>.Failure(InvalidPathMessage, ServiceErrorType.Validation);
        }

        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult<FileListingDto>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        return await _fileSystem.RunAsync(connection.Data!.Context, (session, ct) => GuardAsync(async () =>
        {
            var home = RemotePath.Normalize(session.HomeDirectory) ?? "/";
            var target = requested ?? home;

            var info = await session.GetInfoAsync(target, ct);
            if (info is null)
                return ServiceResult<FileListingDto>.NotFound("Klasör bulunamadı.");
            if (info.Kind is RemoteFileKind.File or RemoteFileKind.Other)
                return ServiceResult<FileListingDto>.Failure("Bu yol bir klasör değil.", ServiceErrorType.Validation);

            var entries = await session.ListAsync(target, ct);
            var users = PosixAccountParser.Parse(await ReadOptionalTextAsync(session, "/etc/passwd", ct));
            var groups = PosixAccountParser.Parse(await ReadOptionalTextAsync(session, "/etc/group", ct));

            return ServiceResult<FileListingDto>.Success(new FileListingDto
            {
                Path = target,
                ParentPath = RemotePath.GetParent(target),
                HomeDirectory = home,
                Breadcrumbs = RemotePath.GetBreadcrumbs(target).Select(c => new FileBreadcrumbDto(c.Name, c.Path)).ToList(),
                Entries = entries
                    .OrderByDescending(e => e.Kind == RemoteFileKind.Directory)
                    .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(e => ToDto(e, users, groups))
                    .ToList()
            });
        }), cancellationToken);
    }

    public async Task<ServiceResult<FileContentDto>> ReadAsync(Guid serverId, string path, CancellationToken cancellationToken = default)
    {
        var target = RemotePath.Normalize(path);
        if (target is null)
            return ServiceResult<FileContentDto>.Failure(InvalidPathMessage, ServiceErrorType.Validation);

        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult<FileContentDto>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        var result = await _fileSystem.RunAsync(connection.Data!.Context, (session, ct) => GuardAsync(async () =>
        {
            var info = await session.GetInfoAsync(target, ct);
            if (info is null)
                return ServiceResult<FileContentDto>.NotFound("Dosya bulunamadı.");
            if (info.Kind == RemoteFileKind.Directory)
                return ServiceResult<FileContentDto>.Failure("Klasörler editörde açılamaz.", ServiceErrorType.Validation);
            if (info.Kind == RemoteFileKind.Other)
                return ServiceResult<FileContentDto>.Failure("Bu dosya türü (aygıt, soket, pipe) editörde açılamaz.", ServiceErrorType.Validation);

            var bytes = await session.ReadAsync(target, MaxEditBytes, ct);
            if (bytes.Length > MaxEditBytes)
                return ServiceResult<FileContentDto>.Failure(TooLargeMessage(), ServiceErrorType.Validation);

            var text = TextFileCodec.Decode(bytes);
            if (text is null)
                return ServiceResult<FileContentDto>.Failure("Bu dosya UTF-8 metin değil; editörde açılamaz. İndirerek inceleyebilirsiniz.", ServiceErrorType.Validation);

            return ServiceResult<FileContentDto>.Success(new FileContentDto
            {
                Path = target,
                Name = RemotePath.GetFileName(target),
                Content = text.Content,
                HasBom = text.HasBom,
                LineEnding = text.LineEnding,
                Size = bytes.Length,
                LastWriteTime = info.LastWriteTimeUtc,
                Version = ContentVersion(bytes)
            });
        }), cancellationToken);

        if (result.IsSuccess)
            await AuditAsync(AuditActions.FileRead, connection.Data, $"Yol: {target}", result, cancellationToken);

        return result;
    }

    public async Task<ServiceResult<FileSaveResultDto>> SaveAsync(Guid serverId, SaveFileDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _saveValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult<FileSaveResultDto>.ValidationFailure(validation);

        var target = RemotePath.Normalize(dto.Path)!;
        byte[] bytes;
        try
        {
            bytes = TextFileCodec.Encode(dto.Content ?? string.Empty, dto.HasBom, dto.LineEnding);
        }
        catch (EncoderFallbackException)
        {
            return ServiceResult<FileSaveResultDto>.Failure("İçerik geçersiz karakterler içeriyor.", ServiceErrorType.Validation);
        }

        if (bytes.Length > MaxEditBytes)
            return ServiceResult<FileSaveResultDto>.Failure(TooLargeMessage(), ServiceErrorType.Validation);

        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult<FileSaveResultDto>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        var result = await _fileSystem.RunAsync(connection.Data!.Context, (session, ct) => GuardAsync(async () =>
        {
            var info = await session.GetInfoAsync(target, ct);
            if (info is null)
                return ServiceResult<FileSaveResultDto>.NotFound("Dosya bulunamadı. Silinmiş veya taşınmış olabilir.");
            if (info.Kind is RemoteFileKind.Directory or RemoteFileKind.Other)
                return ServiceResult<FileSaveResultDto>.Failure("Bu yol düzenlenebilir bir dosya değil.", ServiceErrorType.Validation);

            if (!dto.Force && !string.IsNullOrEmpty(dto.Version))
            {
                var current = await session.ReadAsync(target, MaxEditBytes, ct);
                if (!string.Equals(ContentVersion(current), dto.Version, StringComparison.Ordinal))
                {
                    return ServiceResult<FileSaveResultDto>.Failure(
                        "Dosya siz düzenlerken sunucuda değişti. Değişikliklerinizi yine de kaydetmek için üzerine yazmayı onaylayın.",
                        ServiceErrorType.Conflict);
                }
            }

            await session.WriteAsync(target, bytes, createNew: false, ct);
            var updated = await session.GetInfoAsync(target, ct);
            return ServiceResult<FileSaveResultDto>.Success(
                new FileSaveResultDto(ContentVersion(bytes), bytes.Length, updated?.LastWriteTimeUtc ?? DateTime.UtcNow),
                "Dosya kaydedildi.");
        }), cancellationToken);

        await AuditAsync(AuditActions.FileEdit, connection.Data, $"Yol: {target}, boyut: {bytes.Length} bayt{(dto.Force ? ", üzerine yazıldı" : string.Empty)}", result, cancellationToken);
        return result;
    }

    public async Task<ServiceResult> CreateAsync(Guid serverId, CreateFileEntryDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        var directory = RemotePath.Normalize(dto.Directory)!;
        var target = RemotePath.Combine(directory, dto.Name.Trim());

        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess)
            return connection;

        var result = await _fileSystem.RunAsync(connection.Data!.Context, (session, ct) => GuardAsync(async () =>
        {
            if (await session.GetInfoAsync(target, ct) is not null)
                return ServiceResult<bool>.Failure(ExistsMessage, ServiceErrorType.Conflict);

            if (dto.IsDirectory)
                await session.CreateDirectoryAsync(target, ct);
            else
                await session.WriteAsync(target, [], createNew: true, ct);

            return ServiceResult<bool>.Success(true, dto.IsDirectory ? "Klasör oluşturuldu." : "Dosya oluşturuldu.");
        }), cancellationToken);

        await AuditAsync(dto.IsDirectory ? AuditActions.FileDirectoryCreate : AuditActions.FileCreate, connection.Data, $"Yol: {target}", result, cancellationToken);
        return result;
    }

    public async Task<ServiceResult> MoveAsync(Guid serverId, MoveFileDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _moveValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        var source = RemotePath.Normalize(dto.Source)!;
        var destination = RemotePath.Normalize(dto.Destination)!;
        if (IsProtected(source))
            return ServiceResult.Failure(ProtectedPathMessage, ServiceErrorType.Forbidden);

        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess)
            return connection;

        var resolved = await _fileSystem.ResolveAsync(connection.Data!.Context, source, cancellationToken);
        if (!resolved.IsSuccess)
            return resolved;
        if (IsProtected(resolved.Data!.Entry))
            return ServiceResult.Failure(ProtectedPathMessage, ServiceErrorType.Forbidden);

        var result = await _fileSystem.RunAsync(connection.Data!.Context, (session, ct) => GuardAsync(async () =>
        {
            if (await session.GetInfoAsync(source, ct) is null)
                return ServiceResult<bool>.NotFound("Kaynak dosya veya klasör bulunamadı.");
            if (await session.GetInfoAsync(destination, ct) is not null)
                return ServiceResult<bool>.Failure(ExistsMessage, ServiceErrorType.Conflict);

            await session.RenameAsync(source, destination, ct);
            return ServiceResult<bool>.Success(true, RemotePath.GetParent(source) == RemotePath.GetParent(destination) ? "Yeniden adlandırıldı." : "Taşındı.");
        }), cancellationToken);

        await AuditAsync(AuditActions.FileMove, connection.Data, $"{source} → {destination}", result, cancellationToken);
        return result;
    }

    public async Task<ServiceResult> CopyAsync(Guid serverId, CopyFileDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _copyValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        var source = RemotePath.Normalize(dto.Source)!;
        var destination = RemotePath.Normalize(dto.Destination)!;

        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess)
            return connection;

        var context = connection.Data!.Context;
        var check = await _fileSystem.RunAsync(context, (session, ct) => GuardAsync(async () =>
        {
            if (await session.GetInfoAsync(source, ct) is null)
                return ServiceResult<bool>.NotFound("Kaynak dosya veya klasör bulunamadı.");

            return await session.GetInfoAsync(destination, ct) is null
                ? ServiceResult<bool>.Success(true)
                : ServiceResult<bool>.Failure(ExistsMessage, ServiceErrorType.Conflict);
        }), cancellationToken);

        ServiceResult result = check.IsSuccess
            ? await _fileSystem.CopyAsync(context, source, destination, cancellationToken)
            : check;
        if (result.IsSuccess)
            result = ServiceResult.Success("Kopyalandı.");

        await AuditAsync(AuditActions.FileCopy, connection.Data, $"{source} → {destination}", result, cancellationToken);
        return result;
    }

    public async Task<ServiceResult> DeleteAsync(Guid serverId, DeleteFileDto dto, CancellationToken cancellationToken = default)
    {
        var target = RemotePath.Normalize(dto.Path);
        if (target is null)
            return ServiceResult.Failure(InvalidPathMessage, ServiceErrorType.Validation);
        if (IsProtected(target))
            return ServiceResult.Failure(ProtectedPathMessage, ServiceErrorType.Forbidden);

        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess)
            return connection;

        var context = connection.Data!.Context;
        var resolved = await _fileSystem.ResolveAsync(context, target, cancellationToken);
        if (!resolved.IsSuccess)
            return resolved;

        // Silme son bileşeni izlemez (bağlantı silinirse yalnızca bağlantı gider); üst klasörlerdeki bağlantılar ise çözülür.
        var entry = resolved.Data!.Entry;
        var isLink = resolved.Data.IsSymbolicLink;
        if (IsProtected(entry))
            return ServiceResult.Failure(ProtectedPathMessage, ServiceErrorType.Forbidden);

        var name = RemotePath.GetFileName(target);
        var isDirectory = false;
        var result = await _fileSystem.RunAsync(context, (session, ct) => GuardAsync(async () =>
        {
            var info = await session.GetInfoAsync(entry, ct);
            if (info is null)
                return ServiceResult<bool>.NotFound("Dosya veya klasör bulunamadı.");

            isDirectory = info.Kind == RemoteFileKind.Directory && !isLink;
            if (isDirectory)
            {
                if (IsProtectedTree(entry))
                    return ServiceResult<bool>.Failure(ProtectedTreeMessage, ServiceErrorType.Forbidden);

                return string.Equals(dto.ConfirmationName?.Trim(), name, StringComparison.Ordinal)
                    ? ServiceResult<bool>.Success(true)
                    : ServiceResult<bool>.ValidationFailure(nameof(DeleteFileDto.ConfirmationName), "Klasörü silmek için adını birebir yazın.");
            }

            await session.DeleteFileAsync(entry, ct);
            return ServiceResult<bool>.Success(true);
        }), cancellationToken);

        ServiceResult outcome = result;
        if (result.IsSuccess && isDirectory)
            outcome = await _fileSystem.DeleteRecursiveAsync(context, entry, cancellationToken);
        if (outcome.IsSuccess)
            outcome = ServiceResult.Success(isDirectory ? "Klasör içeriğiyle birlikte silindi." : "Dosya silindi.");

        var path = DescribeResolved(target, entry);
        await AuditAsync(AuditActions.FileDelete, connection.Data, isDirectory ? $"Yol: {path} (klasör, içeriğiyle)" : $"Yol: {path}", outcome, cancellationToken);
        return outcome;
    }

    public async Task<ServiceResult> ChangePermissionsAsync(Guid serverId, ChangePermissionsDto dto, CancellationToken cancellationToken = default)
    {
        var validation = await _permissionsValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        var target = RemotePath.Normalize(dto.Path)!;
        if (IsProtected(target))
            return ServiceResult.Failure(ProtectedPathMessage, ServiceErrorType.Forbidden);

        var mode = TextHelper.NullIfEmpty(dto.Mode);
        var owner = TextHelper.NullIfEmpty(dto.Owner);
        var group = TextHelper.NullIfEmpty(dto.Group);

        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess)
            return connection;

        var context = connection.Data!.Context;
        var resolved = await _fileSystem.ResolveAsync(context, target, cancellationToken);
        if (!resolved.IsSuccess)
            return resolved;

        // chmod/chown bağlantıyı izler; koruma kontrolü ve komut, bağlantının gösterdiği gerçek yolla yapılır.
        var resolvedTarget = resolved.Data!.Target;
        if (IsProtected(resolvedTarget))
            return ServiceResult.Failure(ProtectedPathMessage, ServiceErrorType.Forbidden);
        if (dto.Recursive && resolved.Data.IsSymbolicLink)
            return ServiceResult.Failure("Sembolik bağlantıda alt öğelerle birlikte izin değiştirilemez; bağlantının gösterdiği klasörü seçin.", ServiceErrorType.Validation);
        if (dto.Recursive && IsProtectedTree(resolvedTarget))
            return ServiceResult.Failure(ProtectedTreeMessage, ServiceErrorType.Forbidden);

        ServiceResult result = ServiceResult.Success();
        if (mode is not null)
            result = await _fileSystem.ChangeModeAsync(context, resolvedTarget, mode, dto.Recursive, cancellationToken);
        if (result.IsSuccess && (owner is not null || group is not null))
            result = await _fileSystem.ChangeOwnerAsync(context, resolvedTarget, owner, group, dto.Recursive, cancellationToken);
        if (result.IsSuccess)
            result = ServiceResult.Success("İzinler güncellendi.");

        var details = new StringBuilder($"Yol: {DescribeResolved(target, resolvedTarget)}");
        if (mode is not null)
            details.Append($", mod: {mode}");
        if (owner is not null || group is not null)
            details.Append($", sahip: {owner ?? "-"}:{group ?? "-"}");
        if (dto.Recursive)
            details.Append(", alt öğeler dahil");

        await AuditAsync(AuditActions.FilePermissions, connection.Data, details.ToString(), result, cancellationToken);
        return result;
    }

    public async Task<ServiceResult> UploadAsync(Guid serverId, UploadFileDto dto, Stream content, long length, CancellationToken cancellationToken = default)
    {
        var validation = await _uploadValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ServiceResult.ValidationFailure(validation);

        if (length > MaxUploadBytes)
            return ServiceResult.Failure($"Dosya en fazla {_options.MaxUploadMegabytes} MB olabilir.", ServiceErrorType.Validation);

        var directory = RemotePath.Normalize(dto.Directory)!;
        var target = RemotePath.Combine(directory, dto.FileName.Trim());

        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess)
            return connection;

        var result = await _fileSystem.RunAsync(connection.Data!.Context, (session, ct) => GuardAsync(async () =>
        {
            var directoryInfo = await session.GetInfoAsync(directory, ct);
            if (directoryInfo is null || directoryInfo.Kind is RemoteFileKind.File or RemoteFileKind.Other)
                return ServiceResult<bool>.NotFound("Hedef klasör bulunamadı.");

            var existing = await session.GetInfoAsync(target, ct);
            if (existing is not null)
            {
                if (existing.Kind == RemoteFileKind.Directory)
                    return ServiceResult<bool>.Failure("Hedefte aynı adla bir klasör var.", ServiceErrorType.Validation);
                if (!dto.Overwrite)
                    return ServiceResult<bool>.Failure($"{dto.FileName} zaten var. Üzerine yazmak için onaylayın.", ServiceErrorType.Conflict);
            }

            await session.UploadAsync(content, target, ct);
            return ServiceResult<bool>.Success(true, $"{dto.FileName} yüklendi.");
        }), cancellationToken);

        if (result.ErrorType != ServiceErrorType.Conflict)
            await AuditAsync(AuditActions.FileUpload, connection.Data, $"Yol: {target}, boyut: {length} bayt{(dto.Overwrite ? ", üzerine yazıldı" : string.Empty)}", result, cancellationToken);

        return result;
    }

    public async Task<ServiceResult<RemoteFileStream>> DownloadAsync(Guid serverId, string path, CancellationToken cancellationToken = default)
    {
        var target = RemotePath.Normalize(path);
        if (target is null)
            return ServiceResult<RemoteFileStream>.Failure(InvalidPathMessage, ServiceErrorType.Validation);

        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess)
            return ServiceResult<RemoteFileStream>.Failure(connection.Message ?? "Sunucuya bağlanılamadı.", connection.ErrorType);

        var result = await _fileSystem.OpenReadAsync(connection.Data!.Context, target, cancellationToken);
        await AuditAsync(AuditActions.FileDownload, connection.Data, $"Yol: {target}", result, cancellationToken);
        return result;
    }

    private bool IsProtected(string path) =>
        _options.EffectiveProtectedPaths.Any(p => RemotePath.Normalize(p) == path);

    /// <summary>
    /// Alt öğeleri etkileyen işlemler için: korumalı yolun kendisi, sistem klasörlerinin altındaki her yol ve korumalı
    /// bir yolu içeren her üst klasör (ör. /) reddedilir.
    /// </summary>
    private bool IsProtectedTree(string path) =>
        IsProtected(path)
        || FileManagerOptions.SystemDirectories.Any(root => RemotePath.IsSameOrDescendant(path, root))
        || _options.EffectiveProtectedPaths.Select(RemotePath.Normalize).Any(p => p is not null && RemotePath.IsSameOrDescendant(p, path));

    private static string DescribeResolved(string path, string resolved) =>
        path == resolved ? path : $"{path} → {resolved}";

    private string TooLargeMessage() =>
        $"Dosya editörde açmak için çok büyük (en fazla {_options.MaxEditKilobytes} KB). İndirerek düzenleyebilirsiniz.";

    private static string ContentVersion(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content))[..32];

    private static async Task<string?> ReadOptionalTextAsync(IRemoteFileSession session, string path, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await session.ReadAsync(path, AccountFileMaxBytes, cancellationToken);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (RemoteFileException)
        {
            return null;
        }
    }

    private static FileEntryDto ToDto(RemoteFileInfo info, IReadOnlyDictionary<int, string> users, IReadOnlyDictionary<int, string> groups) => new()
    {
        Name = info.Name,
        Path = info.FullPath,
        Kind = info.Kind,
        Size = info.Size,
        LastWriteTime = info.LastWriteTimeUtc,
        Mode = info.Mode,
        Permissions = FileModes.ToSymbolic(info.Mode),
        OctalMode = FileModes.ToOctal(info.Mode),
        Owner = users.TryGetValue(info.UserId, out var user) ? user : info.UserId.ToString(),
        Group = groups.TryGetValue(info.GroupId, out var group) ? group : info.GroupId.ToString()
    };

    private static async Task<ServiceResult<T>> GuardAsync<T>(Func<Task<ServiceResult<T>>> operation)
    {
        try
        {
            return await operation();
        }
        catch (RemoteFileException ex)
        {
            return ex.Kind switch
            {
                RemoteFileErrorKind.NotFound => ServiceResult<T>.NotFound("Dosya veya klasör bulunamadı."),
                RemoteFileErrorKind.PermissionDenied => ServiceResult<T>.Failure("Sunucuda bu işlem için yetki yok (Permission denied)."),
                RemoteFileErrorKind.AlreadyExists => ServiceResult<T>.Failure(ExistsMessage, ServiceErrorType.Conflict),
                _ => ServiceResult<T>.Failure(string.IsNullOrWhiteSpace(ex.Detail)
                    ? "İşlem sunucuda başarısız oldu."
                    : $"İşlem sunucuda başarısız oldu: {ex.Detail}")
            };
        }
    }

    private async Task AuditAsync(string action, ServerConnection connection, string details, ServiceResult result, CancellationToken cancellationToken)
    {
        if (!result.IsSuccess)
        {
            _logger.LogInformation("Dosya işlemi başarısız. Action: {Action}, ServerId: {ServerId}, Error: {Error}",
                action, connection.ServerId, result.Message);
        }

        await _auditLogService.LogAsync(new AuditEntry(
            action,
            AuditEntityTypes.Server,
            connection.ServerId.ToString(),
            connection.ServerName,
            result.IsSuccess ? details : $"{details} | Hata: {FirstMessage(result)}",
            result.IsSuccess), cancellationToken);
    }

    private static string? FirstMessage(ServiceResult result) =>
        result.Errors.FirstOrDefault()?.Message ?? result.Message;
}
