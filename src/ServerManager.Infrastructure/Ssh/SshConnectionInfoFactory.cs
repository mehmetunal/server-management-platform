using Renci.SshNet;
using ServerManager.Application.DTOs.Ssh;

namespace ServerManager.Infrastructure.Ssh;

internal static class SshConnectionInfoFactory
{
    /// <returns>Bağlantı bilgisi ve bağlantı kapanınca bırakılması gereken kimlik doğrulama nesnesi.</returns>
    /// <exception cref="InvalidOperationException">Kimlik bilgisi eksik veya okunamıyor.</exception>
    public static (ConnectionInfo ConnectionInfo, IDisposable? AuthenticationScope) Create(SshConnectionRequest request, SshOptions options)
    {
        var authenticationMethod = SshAuthenticationFactory.Create(request);
        var connectionInfo = new ConnectionInfo(request.Host, request.Port, request.Username, authenticationMethod)
        {
            Timeout = TimeSpan.FromSeconds(options.ConnectionTimeoutSeconds)
        };

        return (connectionInfo, authenticationMethod as IDisposable);
    }
}
