using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Renci.SshNet.Common;
using ServerManager.Application.DTOs.Ssh;

namespace ServerManager.Infrastructure.Ssh;

internal static class SshErrorTranslator
{
    public static bool TryTranslate(Exception exception, SshConnectionRequest request, ILogger logger, out string message)
    {
        switch (exception)
        {
            case SshAuthenticationException:
                message = "Kimlik doğrulama başarısız. Kullanıcı adı, parola veya anahtarı kontrol edin.";
                return true;

            case SshOperationTimeoutException:
            case OperationCanceledException:
                message = "Bağlantı zaman aşımına uğradı.";
                return true;

            case SocketException socketException:
                logger.LogInformation("SSH soket hatası. Target: {Target}, SocketError: {SocketError}", request, socketException.SocketErrorCode);
                message = $"Sunucuya ulaşılamadı ({socketException.SocketErrorCode}).";
                return true;

            case SshConnectionException connectionException:
                logger.LogInformation("SSH bağlantı hatası. Target: {Target}, Reason: {Reason}", request, connectionException.DisconnectReason);
                message = "SSH bağlantısı kurulamadı.";
                return true;

            case SshException:
                logger.LogWarning("SSH hatası. Target: {Target}, Error: {ErrorType}", request, exception.GetType().Name);
                message = "SSH bağlantısı sırasında bir hata oluştu.";
                return true;

            default:
                message = string.Empty;
                return false;
        }
    }
}
