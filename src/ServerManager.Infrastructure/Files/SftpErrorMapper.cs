using Renci.SshNet.Common;
using ServerManager.Application.Files;

namespace ServerManager.Infrastructure.Files;

internal static class SftpErrorMapper
{
    /// <summary>SFTP sunucusunun işlem hatalarını çevirir; bağlantı hataları (kopma, zaman aşımı) çevrilmez.</summary>
    public static bool TryMap(Exception exception, out RemoteFileException mapped)
    {
        mapped = exception switch
        {
            SftpPathNotFoundException => new RemoteFileException(RemoteFileErrorKind.NotFound, innerException: exception),
            SftpPermissionDeniedException => new RemoteFileException(RemoteFileErrorKind.PermissionDenied, innerException: exception),
            SshException when exception is not (SshConnectionException or SshOperationTimeoutException or SshAuthenticationException)
                => new RemoteFileException(RemoteFileErrorKind.Failure, Describe(exception.Message), exception),
            _ => null!
        };

        return mapped is not null;
    }

    private static string? Describe(string? message) => message switch
    {
        null or "" => null,
        "Failure" => "Sunucu işlemi reddetti (klasör boş değil, hedef var veya dosya sistemi salt okunur olabilir).",
        _ => message.Length > 200 ? message[..200] : message
    };
}
