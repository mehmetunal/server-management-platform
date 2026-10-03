using System.Text;
using Renci.SshNet;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Domain.Enums;

namespace ServerManager.Infrastructure.Ssh;

internal static class SshAuthenticationFactory
{
    public static AuthenticationMethod Create(SshConnectionRequest request)
    {
        switch (request.AuthenticationType)
        {
            case AuthenticationType.Password:
                if (string.IsNullOrEmpty(request.Password))
                    throw new InvalidOperationException("Parola eksik.");
                return new PasswordAuthenticationMethod(request.Username, request.Password);

            case AuthenticationType.PrivateKey:
            case AuthenticationType.PrivateKeyWithPassphrase:
                if (string.IsNullOrWhiteSpace(request.PrivateKey))
                    throw new InvalidOperationException("Private key eksik.");

                using (var keyStream = new MemoryStream(Encoding.UTF8.GetBytes(request.PrivateKey)))
                {
                    var keyFile = string.IsNullOrEmpty(request.Passphrase)
                        ? new PrivateKeyFile(keyStream)
                        : new PrivateKeyFile(keyStream, request.Passphrase);
                    return new PrivateKeyAuthenticationMethod(request.Username, keyFile);
                }

            default:
                throw new InvalidOperationException("Desteklenmeyen kimlik doğrulama yöntemi.");
        }
    }
}
