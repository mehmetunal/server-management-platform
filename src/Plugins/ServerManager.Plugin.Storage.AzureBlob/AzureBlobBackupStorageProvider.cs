using System.Text.RegularExpressions;
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ServerManager.Application.Common;
using ServerManager.Application.Interfaces.Backups;
using ServerManager.Application.Notifications;

namespace ServerManager.Plugin.Storage.AzureBlob;

/// <summary>
/// Azure Blob Storage (ve Azurite) üzerinde yedek saklar. Yükleme başarısız olursa yarım blob silinir.
/// Hesap anahtarı hata metnine yazılmaz.
/// </summary>
public sealed partial class AzureBlobBackupStorageProvider : IBackupStorageProvider
{
    private static readonly IReadOnlyList<NotificationSettingField> FieldDefinitions =
    [
        new(AzureBlobPlugin.AccountNameKey, "Depolama hesabı", NotificationFieldType.Text,
            Hint: "Azure'da 3-24 küçük harf ve rakam. Azurite için devstoreaccount1.",
            Placeholder: "yedekhesabi", MaxLength: 24),
        new(AzureBlobPlugin.AccountKeyKey, "Hesap anahtarı", NotificationFieldType.Secret,
            Hint: "Kaydedildikten sonra gösterilmez. Yalnızca bu kapsayıcıya okuma, yazma ve silme izni yeter.", MaxLength: 200),
        new(AzureBlobPlugin.EndpointKey, "Hizmet adresi", NotificationFieldType.Url, IsRequired: false,
            Hint: "Azure için boş bırakın. Azurite: http://127.0.0.1:10000/devstoreaccount1",
            Placeholder: "https://hesap.blob.core.windows.net", MaxLength: 255),
        new(AzureBlobPlugin.ContainerKey, "Kapsayıcı", NotificationFieldType.Text,
            Hint: "Önceden oluşturulmuş, herkese açık olmayan kapsayıcı.", MaxLength: 63),
        new(AzureBlobPlugin.PrefixKey, "Klasör ön eki", NotificationFieldType.Text, IsRequired: false,
            Hint: "Yedeklerin kapsayıcı içinde yazılacağı klasör (ör. server-manager/). Boşsa kapsayıcı köküne yazılır.",
            Placeholder: "server-manager/", MaxLength: 200)
    ];

    public string SystemName => AzureBlobPlugin.SystemName;

    public string DisplayName => AzureBlobPlugin.DisplayName;

    public string Description => "Azure Blob Storage veya Azurite kapsayıcısına yazar.";

    public IReadOnlyList<NotificationSettingField> Fields => FieldDefinitions;

    public IReadOnlyList<ServiceError> Validate(IReadOnlyDictionary<string, string> settings)
    {
        var errors = new List<ServiceError>();
        if (settings.TryGetValue(AzureBlobPlugin.AccountNameKey, out var account) && !AccountPattern().IsMatch(account))
            errors.Add(new ServiceError(AzureBlobPlugin.AccountNameKey, "Hesap adı 3-24 karakter; yalnızca küçük harf ve rakam içerebilir."));
        if (settings.TryGetValue(AzureBlobPlugin.AccountKeyKey, out var key) && !string.IsNullOrEmpty(key) && !AccountKeyPattern().IsMatch(key))
            errors.Add(new ServiceError(AzureBlobPlugin.AccountKeyKey, "Hesap anahtarı geçersiz karakter içeriyor."));
        if (settings.TryGetValue(AzureBlobPlugin.EndpointKey, out var endpoint) && !TryParseEndpoint(endpoint, out _))
            errors.Add(new ServiceError(AzureBlobPlugin.EndpointKey, "Hizmet adresi http:// veya https:// ile başlamalı; kullanıcı bilgisi ve sorgu içeremez."));
        if (settings.TryGetValue(AzureBlobPlugin.ContainerKey, out var container) && !IsContainerName(container))
            errors.Add(new ServiceError(AzureBlobPlugin.ContainerKey, "Kapsayıcı adı 3-63 karakter; küçük harf, rakam ve tire içerebilir, art arda tire kullanılamaz."));
        if (settings.TryGetValue(AzureBlobPlugin.PrefixKey, out var prefix) && NormalizePrefix(prefix) is null)
            errors.Add(new ServiceError(AzureBlobPlugin.PrefixKey, "Ön ek yalnızca harf, rakam, '.', '_', '-' ve '/' içerebilir; '..' ve '/' ile başlama kullanılamaz."));
        return errors;
    }

    public async Task<ServiceResult> TestAsync(IReadOnlyDictionary<string, string> settings, CancellationToken cancellationToken = default)
    {
        if (!TryRead(settings, out var azure))
            return ServiceResult.Failure("Azure Blob ayarları eksik veya geçersiz.");

        var name = azure.BlobName($".sm-test-{Guid.NewGuid():N}");
        var blob = CreateContainer(azure).GetBlobClient(name);
        try
        {
            using var content = new MemoryStream("server-manager"u8.ToArray());
            await blob.UploadAsync(content, overwrite: true, cancellationToken);
            var download = await blob.DownloadContentAsync(cancellationToken);
            if (download.Value.Content.ToString() != "server-manager")
                return ServiceResult.Failure("Deneme nesnesi okunamadı.");

            await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            return ServiceResult.Success($"Kapsayıcı yazılabilir: {azure.Container}");
        }
        catch (Exception ex) when (IsStorageError(ex, cancellationToken))
        {
            await DeleteQuietlyAsync(blob);
            return ServiceResult.Failure(Describe(ex));
        }
    }

    public async Task<ServiceResult> UploadAsync(IReadOnlyDictionary<string, string> settings, string objectKey, Stream content, CancellationToken cancellationToken = default)
    {
        if (!TryRead(settings, out var azure) || !ObjectKeyPattern().IsMatch(objectKey))
            return ServiceResult.Failure("Azure Blob ayarları veya nesne adı geçersiz.");

        var blob = CreateContainer(azure).GetBlobClient(azure.BlobName(objectKey));
        try
        {
            await blob.UploadAsync(content, overwrite: true, cancellationToken);
            return ServiceResult.Success();
        }
        catch (Exception ex) when (IsStorageError(ex, cancellationToken))
        {
            await DeleteQuietlyAsync(blob);
            return ServiceResult.Failure(Describe(ex));
        }
    }

    public async Task<ServiceResult<Stream>> OpenReadAsync(IReadOnlyDictionary<string, string> settings, string objectKey, CancellationToken cancellationToken = default)
    {
        if (!TryRead(settings, out var azure) || !ObjectKeyPattern().IsMatch(objectKey))
            return ServiceResult<Stream>.Failure("Azure Blob ayarları veya nesne adı geçersiz.");

        var blob = CreateContainer(azure).GetBlobClient(azure.BlobName(objectKey));
        try
        {
            var stream = await blob.OpenReadAsync(new BlobOpenReadOptions(allowModifications: false), cancellationToken);
            return ServiceResult<Stream>.Success(stream);
        }
        catch (RequestFailedException ex) when (ex.Status == 404 && !cancellationToken.IsCancellationRequested)
        {
            return ServiceResult<Stream>.Failure("Yedek dosyası bulunamadı.");
        }
        catch (Exception ex) when (IsStorageError(ex, cancellationToken))
        {
            return ServiceResult<Stream>.Failure(Describe(ex));
        }
    }

    public async Task<ServiceResult> DeleteAsync(IReadOnlyDictionary<string, string> settings, string objectKey, CancellationToken cancellationToken = default)
    {
        if (!TryRead(settings, out var azure) || !ObjectKeyPattern().IsMatch(objectKey))
            return ServiceResult.Failure("Azure Blob ayarları veya nesne adı geçersiz.");

        var blob = CreateContainer(azure).GetBlobClient(azure.BlobName(objectKey));
        try
        {
            await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            return ServiceResult.Success();
        }
        catch (Exception ex) when (IsStorageError(ex, cancellationToken))
        {
            return ServiceResult.Failure(Describe(ex));
        }
    }

    internal static bool TryRead(IReadOnlyDictionary<string, string> settings, out AzureBlobSettings azure)
    {
        azure = null!;
        var endpointText = settings.GetValueOrDefault(AzureBlobPlugin.EndpointKey);
        if (!TryParseEndpoint(endpointText, out var endpoint))
            return false;

        var prefix = NormalizePrefix(settings.GetValueOrDefault(AzureBlobPlugin.PrefixKey));
        if (!settings.TryGetValue(AzureBlobPlugin.AccountNameKey, out var account) || !AccountPattern().IsMatch(account)
            || !settings.TryGetValue(AzureBlobPlugin.AccountKeyKey, out var accountKey) || !AccountKeyPattern().IsMatch(accountKey)
            || !settings.TryGetValue(AzureBlobPlugin.ContainerKey, out var container) || !IsContainerName(container)
            || prefix is null)
            return false;

        var serviceUri = endpoint ?? new Uri($"https://{account}.blob.core.windows.net");
        azure = new AzureBlobSettings(serviceUri, container, prefix, account, accountKey);
        return true;
    }

    /// <returns>Boşsa "", geçerliyse '/' ile biten ön ek; geçersizse null.</returns>
    internal static string? NormalizePrefix(string? prefix)
    {
        var value = prefix?.Trim() ?? string.Empty;
        if (value.Length == 0)
            return string.Empty;
        if (!PrefixPattern().IsMatch(value) || value.StartsWith('/') || value.Split('/').Any(segment => segment is "." or ".."))
            return null;

        value = value.TrimEnd('/');
        return value.Length == 0 ? null : value + "/";
    }

    internal static bool TryParseEndpoint(string? value, out Uri? endpoint)
    {
        endpoint = null;
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var text = value.Trim();
        if (text.Contains("..", StringComparison.Ordinal)
            || !Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment)
            || uri.AbsolutePath.Contains("..", StringComparison.Ordinal))
            return false;

        endpoint = uri;
        return true;
    }

    private static bool IsContainerName(string name) =>
        ContainerPattern().IsMatch(name) && !name.Contains("--", StringComparison.Ordinal);

    private static BlobContainerClient CreateContainer(AzureBlobSettings azure)
    {
        var service = new BlobServiceClient(azure.ServiceUri, new StorageSharedKeyCredential(azure.AccountName, azure.AccountKey));
        return service.GetBlobContainerClient(azure.Container);
    }

    private static async Task DeleteQuietlyAsync(BlobClient blob)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await blob.DeleteIfExistsAsync(cancellationToken: timeout.Token);
        }
        catch (Exception)
        {
            // Yarım blob silinemediyse kapsayıcıda kalır; bir sonraki yükleme aynı adı yazar.
        }
    }

    private static bool IsStorageError(Exception ex, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested && ex is RequestFailedException or HttpRequestException or TaskCanceledException;

    private static string Describe(Exception ex) => ex switch
    {
        RequestFailedException { Status: 403 } => "Azure Blob erişimi reddedildi: hesap adı, anahtar ve kapsayıcı iznini kontrol edin.",
        RequestFailedException { Status: 404, ErrorCode: "ContainerNotFound" } => "Kapsayıcı bulunamadı.",
        RequestFailedException { Status: 404 } => "Yedek dosyası bulunamadı.",
        RequestFailedException failed => $"Azure Blob hatası: {failed.ErrorCode ?? failed.Status.ToString()}",
        TaskCanceledException => "Azure Blob isteği zaman aşımına uğradı.",
        HttpRequestException => "Azure Blob adresine bağlanılamadı; adresi ve ağ erişimini kontrol edin.",
        _ => "Azure Blob isteği başarısız."
    };

    [GeneratedRegex("^[a-z0-9]{3,24}$")]
    private static partial Regex AccountPattern();

    [GeneratedRegex("^[A-Za-z0-9+/=]{20,200}$")]
    private static partial Regex AccountKeyPattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$")]
    private static partial Regex ContainerPattern();

    [GeneratedRegex("^[A-Za-z0-9._/-]{1,200}$")]
    private static partial Regex PrefixPattern();

    [GeneratedRegex(@"^[0-9a-f]{32}/[A-Za-z0-9._-]{1,200}$")]
    private static partial Regex ObjectKeyPattern();
}
