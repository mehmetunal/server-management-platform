using Renci.SshNet;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Infrastructure.Files;

/// <summary>Host key doğrulaması bağlı bir SftpClient ile kimlik doğrulama nesnesinin ömrünü birlikte yönetir.</summary>
internal sealed class SftpClientLease : IDisposable
{
    private readonly IDisposable? _authenticationScope;

    private SftpClientLease(SftpClient client, HostKeyVerifier hostKeyVerifier, IDisposable? authenticationScope)
    {
        Client = client;
        HostKeyVerifier = hostKeyVerifier;
        _authenticationScope = authenticationScope;
    }

    public SftpClient Client { get; }

    public HostKeyVerifier HostKeyVerifier { get; }

    /// <exception cref="InvalidOperationException">Kimlik bilgisi eksik veya okunamıyor.</exception>
    public static SftpClientLease Create(SshConnectionRequest request, SshOptions options, TimeSpan operationTimeout)
    {
        var (connectionInfo, authenticationScope) = SshConnectionInfoFactory.Create(request, options);

        var client = new SftpClient(connectionInfo)
        {
            KeepAliveInterval = TimeSpan.FromSeconds(30),
            OperationTimeout = operationTimeout
        };
        var verifier = new HostKeyVerifier(request.ExpectedHostKeyFingerprint);
        verifier.Attach(client);
        return new SftpClientLease(client, verifier, authenticationScope);
    }

    public async Task ConnectAsync(SshOptions options, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.ConnectionTimeoutSeconds + 5));
        await Client.ConnectAsync(timeoutCts.Token);
    }

    public void Dispose()
    {
        try
        {
            if (Client.IsConnected)
                Client.Disconnect();
        }
        catch (Exception)
        {
            // Bağlantı zaten kopmuş olabilir; kapatma hatası önemsizdir.
        }

        Client.Dispose();
        _authenticationScope?.Dispose();
    }
}
