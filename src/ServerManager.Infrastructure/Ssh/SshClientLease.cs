using Renci.SshNet;
using ServerManager.Application.DTOs.Ssh;

namespace ServerManager.Infrastructure.Ssh;

/// <summary>Host key doğrulaması bağlı bir SshClient ile kimlik doğrulama nesnesinin ömrünü birlikte yönetir.</summary>
internal sealed class SshClientLease : IDisposable
{
    private readonly IDisposable? _authenticationScope;

    private SshClientLease(SshClient client, HostKeyVerifier hostKeyVerifier, IDisposable? authenticationScope)
    {
        Client = client;
        HostKeyVerifier = hostKeyVerifier;
        _authenticationScope = authenticationScope;
    }

    public SshClient Client { get; }

    public HostKeyVerifier HostKeyVerifier { get; }

    /// <exception cref="InvalidOperationException">Kimlik bilgisi eksik veya okunamıyor.</exception>
    public static SshClientLease Create(SshConnectionRequest request, SshOptions options)
    {
        var authenticationMethod = SshAuthenticationFactory.Create(request);
        var connectionInfo = new ConnectionInfo(request.Host, request.Port, request.Username, authenticationMethod)
        {
            Timeout = TimeSpan.FromSeconds(options.ConnectionTimeoutSeconds)
        };

        var client = new SshClient(connectionInfo)
        {
            KeepAliveInterval = TimeSpan.FromSeconds(30)
        };
        var verifier = new HostKeyVerifier(request.ExpectedHostKeyFingerprint);
        verifier.Attach(client);
        return new SshClientLease(client, verifier, authenticationMethod as IDisposable);
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
