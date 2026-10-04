using System.Globalization;
using System.Text.RegularExpressions;
using ServerManager.Application.Deployments;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Backups;

/// <summary>Depolamadaki nesne adları. Silme yalnızca bu kalıba uyan ve işin klasöründeki nesnelere yapılır.</summary>
public static partial class BackupNames
{
    private const string TimestampFormat = "yyyyMMdd-HHmmss";

    public static string Extension(BackupSourceType sourceType, bool encrypted) =>
        (sourceType == BackupSourceType.Database ? ".sql.gz" : ".tar.gz") + (encrypted ? BackupEncryption.FileExtension : string.Empty);

    public static string ObjectKey(Guid jobId, Guid runId, DateTime startedAtUtc, BackupSourceType sourceType, bool encrypted) =>
        $"{jobId:N}/{startedAtUtc.ToString(TimestampFormat, CultureInfo.InvariantCulture)}-{runId.ToString("N")[..8]}{Extension(sourceType, encrypted)}";

    public static string FileName(string jobName, DateTime startedAtUtc, BackupSourceType sourceType, bool encrypted) =>
        $"{DeploymentNames.Slugify(jobName)}-{startedAtUtc.ToString(TimestampFormat, CultureInfo.InvariantCulture)}{Extension(sourceType, encrypted)}";

    /// <summary>Şifresi çözülerek indirilen dosyanın adı (.smbk eki olmadan).</summary>
    public static string DecryptedFileName(string fileName) =>
        fileName.EndsWith(BackupEncryption.FileExtension, StringComparison.Ordinal) ? fileName[..^BackupEncryption.FileExtension.Length] : fileName;

    public static bool IsJobObjectKey(Guid jobId, string? objectKey) =>
        objectKey is not null
        && objectKey.StartsWith($"{jobId:N}/", StringComparison.Ordinal)
        && ObjectKeyPattern().IsMatch(objectKey);

    [GeneratedRegex(@"^[0-9a-f]{32}/\d{8}-\d{6}-[0-9a-f]{8}\.(tar|sql)\.gz(\.smbk)?$")]
    private static partial Regex ObjectKeyPattern();
}
