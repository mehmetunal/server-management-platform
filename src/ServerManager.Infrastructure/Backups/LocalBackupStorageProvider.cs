using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using ServerManager.Application.Backups;
using ServerManager.Application.Common;
using ServerManager.Application.Interfaces.Backups;
using ServerManager.Application.Notifications;

namespace ServerManager.Infrastructure.Backups;

/// <summary>
/// Yedekleri panelin çalıştığı makinede saklar. Klasör yalnızca <see cref="BackupOptions.LocalRootPath"/> altında olabilir;
/// panel diskine rastgele yol yazılamaz. Yükleme önce .partial dosyasına yapılır, başarılı olunca yeniden adlandırılır.
/// </summary>
public sealed partial class LocalBackupStorageProvider : IBackupStorageProvider
{
    public const string ProviderSystemName = "Storage.Local";
    public const string FolderKey = "folder";

    private const string PartialExtension = ".partial";
    private const int CopyBufferSize = 1024 * 1024;

    private static readonly IReadOnlyList<NotificationSettingField> FieldDefinitions =
    [
        new(FolderKey, "Klasör adı", NotificationFieldType.Text,
            Hint: "Panel sunucusunda Backup:LocalRootPath altındaki klasör (ör. gunluk). Panel diski dolarsa yedek alınamaz; uzun süreli saklama için S3 uyumlu depolama önerilir.",
            Placeholder: "gunluk",
            MaxLength: 64)
    ];

    private readonly IOptions<BackupOptions> _options;

    public LocalBackupStorageProvider(IOptions<BackupOptions> options)
    {
        _options = options;
    }

    public string SystemName => ProviderSystemName;

    public string DisplayName => "Yerel disk (panel sunucusu)";

    public string Description => "Yedekleri panelin çalıştığı makinedeki bir klasörde saklar.";

    public IReadOnlyList<NotificationSettingField> Fields => FieldDefinitions;

    public IReadOnlyList<ServiceError> Validate(IReadOnlyDictionary<string, string> settings)
    {
        if (settings.TryGetValue(FolderKey, out var folder) && !FolderPattern().IsMatch(folder))
            return [new ServiceError(FolderKey, "Klasör adı harf veya rakamla başlamalı; yalnızca harf, rakam, nokta, tire ve alt çizgi içerebilir.")];

        return [];
    }

    public async Task<ServiceResult> TestAsync(IReadOnlyDictionary<string, string> settings, CancellationToken cancellationToken = default)
    {
        if (!TryGetFolder(settings, out var folder))
            return ServiceResult.Failure("Klasör ayarı geçersiz.");

        var path = Path.Combine(folder, $".sm-test-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(path, "server-manager", cancellationToken);
            var content = await File.ReadAllTextAsync(path, cancellationToken);
            File.Delete(path);
            return content == "server-manager"
                ? ServiceResult.Success($"Klasör yazılabilir: {folder}")
                : ServiceResult.Failure("Deneme dosyası okunamadı.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDelete(path);
            return ServiceResult.Failure($"Klasöre yazılamadı: {ex.Message}");
        }
    }

    public async Task<ServiceResult> UploadAsync(IReadOnlyDictionary<string, string> settings, string objectKey, Stream content, CancellationToken cancellationToken = default)
    {
        if (!TryGetPath(settings, objectKey, out var path))
            return ServiceResult.Failure("Yedek dosyası yolu geçersiz.");

        var partial = path + PartialExtension;
        var contentFailed = false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using (var file = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None, CopyBufferSize, useAsync: true))
            {
                var buffer = new byte[CopyBufferSize];
                while (true)
                {
                    int read;
                    try
                    {
                        read = await content.ReadAsync(buffer, cancellationToken);
                    }
                    catch
                    {
                        contentFailed = true;
                        throw;
                    }

                    if (read == 0)
                        break;
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }

                await file.FlushAsync(cancellationToken);
            }

            File.Move(partial, path);
            return ServiceResult.Success();
        }
        catch (Exception ex) when (!contentFailed && ex is IOException or UnauthorizedAccessException && !cancellationToken.IsCancellationRequested)
        {
            TryDelete(partial);
            return ServiceResult.Failure($"Yedek dosyası yazılamadı: {ex.Message}");
        }
        catch
        {
            TryDelete(partial);
            throw;
        }
    }

    public Task<ServiceResult<Stream>> OpenReadAsync(IReadOnlyDictionary<string, string> settings, string objectKey, CancellationToken cancellationToken = default)
    {
        if (!TryGetPath(settings, objectKey, out var path))
            return Task.FromResult(ServiceResult<Stream>.Failure("Yedek dosyası yolu geçersiz."));

        try
        {
            Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, useAsync: true);
            return Task.FromResult(ServiceResult<Stream>.Success(stream));
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return Task.FromResult(ServiceResult<Stream>.Failure("Yedek dosyası depolamada bulunamadı."));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(ServiceResult<Stream>.Failure($"Yedek dosyası okunamadı: {ex.Message}"));
        }
    }

    public Task<ServiceResult> DeleteAsync(IReadOnlyDictionary<string, string> settings, string objectKey, CancellationToken cancellationToken = default)
    {
        if (!TryGetPath(settings, objectKey, out var path))
            return Task.FromResult(ServiceResult.Failure("Yedek dosyası yolu geçersiz."));

        try
        {
            File.Delete(path);
            return Task.FromResult(ServiceResult.Success());
        }
        catch (DirectoryNotFoundException)
        {
            return Task.FromResult(ServiceResult.Success());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Task.FromResult(ServiceResult.Failure($"Yedek dosyası silinemedi: {ex.Message}"));
        }
    }

    public string RootPath => Path.GetFullPath(_options.Value.LocalRootPath);

    private bool TryGetFolder(IReadOnlyDictionary<string, string> settings, out string folder)
    {
        folder = string.Empty;
        if (!settings.TryGetValue(FolderKey, out var name) || !FolderPattern().IsMatch(name))
            return false;

        var root = RootPath;
        folder = Path.GetFullPath(Path.Combine(root, name));
        return IsInside(root, folder);
    }

    private bool TryGetPath(IReadOnlyDictionary<string, string> settings, string objectKey, out string path)
    {
        path = string.Empty;
        if (!TryGetFolder(settings, out var folder) || !ObjectKeyPattern().IsMatch(objectKey))
            return false;

        path = Path.GetFullPath(Path.Combine(folder, objectKey.Replace('/', Path.DirectorySeparatorChar)));
        return IsInside(folder, path);
    }

    private static bool IsInside(string root, string path)
    {
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Yarım dosya silinemezse sonraki denemede .partial adı yine kullanılmaz (her çalışmanın adı farklıdır).
        }
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex FolderPattern();

    [GeneratedRegex(@"^[0-9a-f]{32}/[A-Za-z0-9._-]{1,200}$")]
    private static partial Regex ObjectKeyPattern();
}
