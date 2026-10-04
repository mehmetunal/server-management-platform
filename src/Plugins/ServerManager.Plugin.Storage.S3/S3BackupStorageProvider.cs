using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using ServerManager.Application.Common;
using ServerManager.Application.Interfaces.Backups;
using ServerManager.Application.Notifications;

namespace ServerManager.Plugin.Storage.S3;

/// <summary>
/// S3 API'si üzerinden yedek saklar (AWS S3, Cloudflare R2, MinIO, Backblaze B2…). Boyutu bilinmeyen akış
/// sabit boyutlu parçalarla çok parçalı yüklenir; hata olursa yükleme iptal edilir ve yarım nesne kalmaz.
/// </summary>
public sealed partial class S3BackupStorageProvider : IBackupStorageProvider
{
    public const int PartSize = 16 * 1024 * 1024;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(10);

    private static readonly IReadOnlyList<NotificationSettingField> FieldDefinitions =
    [
        new(S3Plugin.EndpointKey, "Uç nokta (endpoint)", NotificationFieldType.Url, IsRequired: false,
            Hint: "Amazon S3 için boş bırakın. Cloudflare R2: https://<hesap-id>.r2.cloudflarestorage.com, MinIO: http://sunucu:9000, Backblaze B2: https://s3.<bölge>.backblazeb2.com",
            Placeholder: "https://…", MaxLength: 255),
        new(S3Plugin.RegionKey, "Bölge", NotificationFieldType.Text,
            Hint: "AWS bölgesi (ör. eu-central-1). Cloudflare R2 için auto, MinIO için genelde us-east-1.",
            Placeholder: S3Plugin.DefaultRegion, DefaultValue: S3Plugin.DefaultRegion, MaxLength: 32),
        new(S3Plugin.BucketKey, "Bucket", NotificationFieldType.Text,
            Hint: "Önceden oluşturulmuş bucket. Herkese açık olmamalıdır.", MaxLength: 63),
        new(S3Plugin.PrefixKey, "Klasör ön eki", NotificationFieldType.Text, IsRequired: false,
            Hint: "Yedeklerin bucket içinde yazılacağı klasör (ör. server-manager/). Boşsa bucket köküne yazılır.",
            Placeholder: "server-manager/", MaxLength: 200),
        new(S3Plugin.AccessKeyKey, "Erişim anahtarı (Access Key ID)", NotificationFieldType.Text, MaxLength: 128),
        new(S3Plugin.SecretKeyKey, "Gizli anahtar (Secret Access Key)", NotificationFieldType.Secret,
            Hint: "Yalnızca bu bucket'a okuma, yazma ve silme izni olan bir anahtar kullanın. Kaydedildikten sonra gösterilmez.", MaxLength: 256),
        new(S3Plugin.PathStyleKey, "Adres biçimi", NotificationFieldType.Select,
            Hint: "MinIO ve bazı uyumlu servisler yol biçimi ister.",
            DefaultValue: "false",
            Options: [new NotificationFieldOption("false", "Sanal barındırma (bucket.uç-nokta)"), new NotificationFieldOption("true", "Yol biçimi (uç-nokta/bucket)")])
    ];

    public string SystemName => S3Plugin.SystemName;

    public string DisplayName => S3Plugin.DisplayName;

    public string Description => "Amazon S3, Cloudflare R2, MinIO, Backblaze B2 gibi S3 API'si sunan nesne depolamalarına yazar.";

    public IReadOnlyList<NotificationSettingField> Fields => FieldDefinitions;

    public IReadOnlyList<ServiceError> Validate(IReadOnlyDictionary<string, string> settings)
    {
        var errors = new List<ServiceError>();
        if (settings.TryGetValue(S3Plugin.EndpointKey, out var endpoint) && !TryParseEndpoint(endpoint, out _))
            errors.Add(new ServiceError(S3Plugin.EndpointKey, "Uç nokta http:// veya https:// ile başlayan bir adres olmalıdır (yol içermeden)."));
        if (settings.TryGetValue(S3Plugin.RegionKey, out var region) && !RegionPattern().IsMatch(region))
            errors.Add(new ServiceError(S3Plugin.RegionKey, "Bölge yalnızca küçük harf, rakam ve tire içerebilir (ör. eu-central-1, auto)."));
        if (settings.TryGetValue(S3Plugin.BucketKey, out var bucket) && !BucketPattern().IsMatch(bucket))
            errors.Add(new ServiceError(S3Plugin.BucketKey, "Bucket adı 3-63 karakter; küçük harf, rakam, nokta ve tire içerebilir."));
        if (settings.TryGetValue(S3Plugin.PrefixKey, out var prefix) && NormalizePrefix(prefix) is null)
            errors.Add(new ServiceError(S3Plugin.PrefixKey, "Ön ek yalnızca harf, rakam, '.', '_', '-' ve '/' içerebilir; '..' ve '/' ile başlama kullanılamaz."));
        if (settings.TryGetValue(S3Plugin.AccessKeyKey, out var accessKey) && !AccessKeyPattern().IsMatch(accessKey))
            errors.Add(new ServiceError(S3Plugin.AccessKeyKey, "Erişim anahtarı geçersiz karakter içeriyor."));
        if (settings.TryGetValue(S3Plugin.PathStyleKey, out var pathStyle) && pathStyle is not ("true" or "false"))
            errors.Add(new ServiceError(S3Plugin.PathStyleKey, "Adres biçimini seçin."));
        return errors;
    }

    public async Task<ServiceResult> TestAsync(IReadOnlyDictionary<string, string> settings, CancellationToken cancellationToken = default)
    {
        if (!TryRead(settings, out var s3))
            return ServiceResult.Failure("S3 ayarları eksik veya geçersiz.");

        var key = s3.Key($".sm-test-{Guid.NewGuid():N}");
        using var client = CreateClient(s3);
        try
        {
            await client.PutObjectAsync(new PutObjectRequest { BucketName = s3.Bucket, Key = key, ContentBody = "server-manager" }, cancellationToken);
            using (var response = await client.GetObjectAsync(new GetObjectRequest { BucketName = s3.Bucket, Key = key }, cancellationToken))
            using (var reader = new StreamReader(response.ResponseStream, Encoding.UTF8))
            {
                if (await reader.ReadToEndAsync(cancellationToken) != "server-manager")
                    return ServiceResult.Failure("Deneme nesnesi okunamadı.");
            }

            await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = s3.Bucket, Key = key }, cancellationToken);
            return ServiceResult.Success($"Bucket yazılabilir: {s3.Bucket}");
        }
        catch (Exception ex) when (IsStorageError(ex, cancellationToken))
        {
            return ServiceResult.Failure(Describe(ex));
        }
    }

    public async Task<ServiceResult> UploadAsync(IReadOnlyDictionary<string, string> settings, string objectKey, Stream content, CancellationToken cancellationToken = default)
    {
        if (!TryRead(settings, out var s3) || !ObjectKeyPattern().IsMatch(objectKey))
            return ServiceResult.Failure("S3 ayarları veya nesne adı geçersiz.");

        var key = s3.Key(objectKey);
        using var client = CreateClient(s3);
        var buffer = new byte[PartSize];

        var length = await FillAsync(content, buffer, cancellationToken);
        if (length < PartSize)
        {
            try
            {
                using var single = new MemoryStream(buffer, 0, length, writable: false);
                await client.PutObjectAsync(new PutObjectRequest { BucketName = s3.Bucket, Key = key, InputStream = single, AutoCloseStream = false }, cancellationToken);
                return ServiceResult.Success();
            }
            catch (Exception ex) when (IsStorageError(ex, cancellationToken))
            {
                return ServiceResult.Failure(Describe(ex));
            }
        }

        string uploadId;
        try
        {
            var initiated = await client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest { BucketName = s3.Bucket, Key = key }, cancellationToken);
            uploadId = initiated.UploadId;
        }
        catch (Exception ex) when (IsStorageError(ex, cancellationToken))
        {
            return ServiceResult.Failure(Describe(ex));
        }

        var parts = new List<PartETag>();
        try
        {
            var partNumber = 1;
            while (length > 0)
            {
                using (var part = new MemoryStream(buffer, 0, length, writable: false))
                {
                    var uploaded = await client.UploadPartAsync(new UploadPartRequest
                    {
                        BucketName = s3.Bucket,
                        Key = key,
                        UploadId = uploadId,
                        PartNumber = partNumber,
                        PartSize = length,
                        InputStream = part
                    }, cancellationToken);
                    parts.Add(new PartETag { PartNumber = partNumber, ETag = uploaded.ETag });
                }

                partNumber++;
                length = await FillAsync(content, buffer, cancellationToken);
            }

            await client.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
            {
                BucketName = s3.Bucket,
                Key = key,
                UploadId = uploadId,
                PartETags = parts
            }, cancellationToken);
            return ServiceResult.Success();
        }
        catch (Exception ex)
        {
            await AbortAsync(client, s3, key, uploadId);
            if (IsStorageError(ex, cancellationToken))
                return ServiceResult.Failure(Describe(ex));
            throw;
        }
    }

    public async Task<ServiceResult<Stream>> OpenReadAsync(IReadOnlyDictionary<string, string> settings, string objectKey, CancellationToken cancellationToken = default)
    {
        if (!TryRead(settings, out var s3) || !ObjectKeyPattern().IsMatch(objectKey))
            return ServiceResult<Stream>.Failure("S3 ayarları veya nesne adı geçersiz.");

        var client = CreateClient(s3);
        try
        {
            var response = await client.GetObjectAsync(new GetObjectRequest { BucketName = s3.Bucket, Key = s3.Key(objectKey) }, cancellationToken);
            return ServiceResult<Stream>.Success(new S3ObjectStream(response, client));
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            client.Dispose();
            return ServiceResult<Stream>.Failure("Yedek dosyası depolamada bulunamadı.");
        }
        catch (Exception ex) when (IsStorageError(ex, cancellationToken))
        {
            client.Dispose();
            return ServiceResult<Stream>.Failure(Describe(ex));
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public async Task<ServiceResult> DeleteAsync(IReadOnlyDictionary<string, string> settings, string objectKey, CancellationToken cancellationToken = default)
    {
        if (!TryRead(settings, out var s3) || !ObjectKeyPattern().IsMatch(objectKey))
            return ServiceResult.Failure("S3 ayarları veya nesne adı geçersiz.");

        using var client = CreateClient(s3);
        try
        {
            await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = s3.Bucket, Key = s3.Key(objectKey) }, cancellationToken);
            return ServiceResult.Success();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return ServiceResult.Success();
        }
        catch (Exception ex) when (IsStorageError(ex, cancellationToken))
        {
            return ServiceResult.Failure(Describe(ex));
        }
    }

    internal static bool TryRead(IReadOnlyDictionary<string, string> settings, out S3Settings s3)
    {
        s3 = null!;
        var endpointText = settings.GetValueOrDefault(S3Plugin.EndpointKey);
        Uri? endpoint = null;
        if (!string.IsNullOrWhiteSpace(endpointText) && !TryParseEndpoint(endpointText, out endpoint))
            return false;

        var region = settings.GetValueOrDefault(S3Plugin.RegionKey) is { Length: > 0 } r ? r : S3Plugin.DefaultRegion;
        var prefix = NormalizePrefix(settings.GetValueOrDefault(S3Plugin.PrefixKey));
        if (!settings.TryGetValue(S3Plugin.BucketKey, out var bucket) || !BucketPattern().IsMatch(bucket)
            || !settings.TryGetValue(S3Plugin.AccessKeyKey, out var accessKey) || string.IsNullOrEmpty(accessKey)
            || !settings.TryGetValue(S3Plugin.SecretKeyKey, out var secretKey) || string.IsNullOrEmpty(secretKey)
            || !RegionPattern().IsMatch(region) || prefix is null)
            return false;

        s3 = new S3Settings(endpoint, region, bucket, prefix, accessKey, secretKey, settings.GetValueOrDefault(S3Plugin.PathStyleKey) == "true");
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

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(uri.Query) || uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.UserInfo))
            return false;

        endpoint = uri;
        return true;
    }

    private static AmazonS3Client CreateClient(S3Settings s3)
    {
        var config = new AmazonS3Config
        {
            ForcePathStyle = s3.PathStyle,
            Timeout = RequestTimeout,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        };

        if (s3.Endpoint is null)
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(s3.Region);
        }
        else
        {
            config.ServiceURL = s3.Endpoint.AbsoluteUri.TrimEnd('/');
            config.AuthenticationRegion = s3.Region;
        }

        return new AmazonS3Client(new BasicAWSCredentials(s3.AccessKey, s3.SecretKey), config);
    }

    private static async Task<int> FillAsync(Stream content, byte[] buffer, CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await content.ReadAsync(buffer.AsMemory(total), cancellationToken);
            if (read == 0)
                break;
            total += read;
        }
        return total;
    }

    private static async Task AbortAsync(AmazonS3Client client, S3Settings s3, string key, string uploadId)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest { BucketName = s3.Bucket, Key = key, UploadId = uploadId }, timeout.Token);
        }
        catch (Exception)
        {
            // İptal edilemeyen yükleme için bucket'ta "tamamlanmamış çok parçalı yüklemeleri sil" yaşam döngüsü kuralı önerilir (README).
        }
    }

    private static bool IsStorageError(Exception ex, CancellationToken cancellationToken) =>
        !cancellationToken.IsCancellationRequested && ex is AmazonServiceException or AmazonClientException or HttpRequestException or TaskCanceledException;

    private static string Describe(Exception ex) => ex switch
    {
        AmazonS3Exception { StatusCode: HttpStatusCode.Forbidden } or AmazonS3Exception { ErrorCode: "InvalidAccessKeyId" or "SignatureDoesNotMatch" } =>
            "S3 erişimi reddedildi: anahtarları ve bucket iznini kontrol edin.",
        AmazonS3Exception { ErrorCode: "NoSuchBucket" } => "Bucket bulunamadı.",
        AmazonS3Exception s3 => $"S3 hatası: {s3.ErrorCode ?? ((int)s3.StatusCode).ToString()} {TextHelper.Truncate(s3.Message, 200)}",
        TaskCanceledException => "S3 isteği zaman aşımına uğradı.",
        HttpRequestException or AmazonClientException => "S3 uç noktasına bağlanılamadı; adresi ve ağ erişimini kontrol edin.",
        _ => "S3 isteği başarısız."
    };

    [GeneratedRegex("^[a-z0-9-]{2,32}$")]
    private static partial Regex RegionPattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{1,61}[a-z0-9]$")]
    private static partial Regex BucketPattern();

    [GeneratedRegex("^[A-Za-z0-9._/-]{1,200}$")]
    private static partial Regex PrefixPattern();

    [GeneratedRegex("^[A-Za-z0-9/+=_-]{3,128}$")]
    private static partial Regex AccessKeyPattern();

    [GeneratedRegex(@"^[0-9a-f]{32}/[A-Za-z0-9._-]{1,200}$")]
    private static partial Regex ObjectKeyPattern();
}
