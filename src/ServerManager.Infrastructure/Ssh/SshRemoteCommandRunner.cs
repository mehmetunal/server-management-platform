using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet.Common;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Ssh;

namespace ServerManager.Infrastructure.Ssh;

public sealed class SshRemoteCommandRunner : IRemoteCommandRunner
{
    private readonly SshOptions _options;
    private readonly ILogger<SshRemoteCommandRunner> _logger;

    public SshRemoteCommandRunner(IOptions<SshOptions> options, ILogger<SshRemoteCommandRunner> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ServiceResult<T>> RunAsync<T>(
        RemoteExecutionContext context,
        Func<IRemoteCommandExecutor, CancellationToken, Task<ServiceResult<T>>> work,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(context.Connection.ExpectedHostKeyFingerprint))
            return ServiceResult<T>.Failure("Host key henüz doğrulanmadı. Önce bağlantı testi yapın.");

        SshClientLease lease;
        try
        {
            lease = SshClientLease.Create(context.Connection, _options);
        }
        catch (Exception ex) when (ex is SshException or InvalidOperationException or ArgumentException)
        {
            _logger.LogWarning("SSH kimlik bilgisi hazırlanamadı. Target: {Target}, Error: {ErrorType}", context, ex.GetType().Name);
            return ServiceResult<T>.Failure("Kimlik bilgisi okunamadı.");
        }

        using (lease)
        {
            try
            {
                await lease.ConnectAsync(_options, cancellationToken);
                return await work(new SshCommandExecutor(lease.Client, context), cancellationToken);
            }
            catch (Exception ex) when (lease.HostKeyVerifier.Mismatch)
            {
                _logger.LogWarning("Host key fingerprint uyuşmuyor. Target: {Target}, Error: {ErrorType}", context, ex.GetType().Name);
                return ServiceResult<T>.Failure(HostKeyVerifier.MismatchMessage);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested
                                       && SshErrorTranslator.TryTranslate(ex, context.Connection, _logger, out var message))
            {
                return ServiceResult<T>.Failure(message);
            }
        }
    }
}
