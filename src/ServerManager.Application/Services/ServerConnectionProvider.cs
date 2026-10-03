using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Application.Interfaces.Ssh;

namespace ServerManager.Application.Services;

public class ServerConnectionProvider : IServerConnectionProvider
{
    private readonly IServerRepository _serverRepository;
    private readonly ISecretProtector _secretProtector;
    private readonly ILogger<ServerConnectionProvider> _logger;

    public ServerConnectionProvider(IServerRepository serverRepository, ISecretProtector secretProtector, ILogger<ServerConnectionProvider> logger)
    {
        _serverRepository = serverRepository;
        _secretProtector = secretProtector;
        _logger = logger;
    }

    public async Task<ServiceResult<ServerConnection>> GetAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetWithDetailsAsync(serverId, cancellationToken);
        if (server is null)
            return ServiceResult<ServerConnection>.NotFound("Sunucu bulunamadı.");

        if (server.HostKeyFingerprint is null)
            return ServiceResult<ServerConnection>.Failure("Host key henüz doğrulanmadı. Önce \"Bağlantıyı Test Et\" ile sunucuyu doğrulayın.");

        if (server.Credential is null)
            return ServiceResult<ServerConnection>.Failure("Bu sunucu için kayıtlı kimlik bilgisi bulunamadı.");

        try
        {
            var context = new RemoteExecutionContext
            {
                Connection = new SshConnectionRequest
                {
                    Host = server.IpAddress,
                    Port = server.SshPort,
                    Username = server.Username,
                    AuthenticationType = server.AuthenticationType,
                    Password = Decrypt(server.Credential.EncryptedPassword),
                    PrivateKey = Decrypt(server.Credential.EncryptedPrivateKey),
                    Passphrase = Decrypt(server.Credential.EncryptedPassphrase),
                    ExpectedHostKeyFingerprint = server.HostKeyFingerprint
                },
                UseSudo = server.UseSudo,
                SudoPassword = server.UseSudo ? Decrypt(server.Credential.EncryptedSudoPassword) : null
            };

            return ServiceResult<ServerConnection>.Success(new ServerConnection
            {
                ServerId = server.Id,
                ServerName = server.Name,
                Context = context
            });
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Sunucu kimlik bilgileri çözülemedi. ServerId: {ServerId}", server.Id);
            return ServiceResult<ServerConnection>.Failure("Kimlik bilgileri çözülemedi. Master key değişmiş olabilir.");
        }
    }

    private string? Decrypt(string? protectedValue) =>
        string.IsNullOrEmpty(protectedValue) ? null : _secretProtector.Unprotect(protectedValue);
}
