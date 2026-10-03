using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet;
using Renci.SshNet.Common;
using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Monitoring;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Infrastructure.Monitoring;

public sealed class SshMetricsCollector : IMetricsCollector
{
    private readonly SshOptions _options;
    private readonly ILogger<SshMetricsCollector> _logger;

    public SshMetricsCollector(IOptions<SshOptions> options, ILogger<SshMetricsCollector> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<MetricsCollectionResult> CollectAsync(SshConnectionRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrEmpty(request.ExpectedHostKeyFingerprint))
            return Failure("Host key henüz doğrulanmadı. Önce bağlantı testi yapın.", stopwatch);

        AuthenticationMethod authenticationMethod;
        try
        {
            authenticationMethod = SshAuthenticationFactory.Create(request);
        }
        catch (Exception ex) when (ex is SshException or InvalidOperationException or ArgumentException)
        {
            _logger.LogWarning("SSH kimlik bilgisi hazırlanamadı. Target: {Target}, Error: {ErrorType}", request, ex.GetType().Name);
            return Failure("Kimlik bilgisi okunamadı.", stopwatch);
        }

        using var authenticationMethodScope = authenticationMethod as IDisposable;
        var connectionInfo = new ConnectionInfo(request.Host, request.Port, request.Username, authenticationMethod)
        {
            Timeout = TimeSpan.FromSeconds(_options.ConnectionTimeoutSeconds)
        };

        using var client = new SshClient(connectionInfo);
        var hostKeyVerifier = new HostKeyVerifier(request.ExpectedHostKeyFingerprint);
        hostKeyVerifier.Attach(client);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.ConnectionTimeoutSeconds + _options.CommandTimeoutSeconds + 10));

            await client.ConnectAsync(timeoutCts.Token);

            using var command = client.CreateCommand(LinuxMetricsScript.Command);
            command.CommandTimeout = TimeSpan.FromSeconds(_options.CommandTimeoutSeconds + 5);
            await command.ExecuteAsync(timeoutCts.Token);
            var output = command.Result;
            client.Disconnect();

            var snapshot = LinuxMetricsParser.Parse(output, DateTime.UtcNow);
            stopwatch.Stop();
            return new MetricsCollectionResult
            {
                IsSuccess = true,
                Message = "Metrikler toplandı.",
                DurationMs = stopwatch.ElapsedMilliseconds,
                Snapshot = snapshot
            };
        }
        catch (Exception ex) when (hostKeyVerifier.Mismatch)
        {
            _logger.LogWarning("Host key fingerprint uyuşmuyor. Target: {Target}, Error: {ErrorType}", request, ex.GetType().Name);
            stopwatch.Stop();
            return new MetricsCollectionResult
            {
                IsSuccess = false,
                FingerprintMismatch = true,
                Message = HostKeyVerifier.MismatchMessage,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (FormatException ex)
        {
            _logger.LogWarning("Metrik çıktısı ayrıştırılamadı. Target: {Target}, Error: {Error}", request, ex.Message);
            return Failure("Metrik çıktısı okunamadı. Sunucunun Linux olduğundan emin olun.", stopwatch);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested
                                   && SshErrorTranslator.TryTranslate(ex, request, _logger, out var message))
        {
            return Failure(message, stopwatch);
        }
    }

    private static MetricsCollectionResult Failure(string message, Stopwatch stopwatch)
    {
        stopwatch.Stop();
        return new MetricsCollectionResult
        {
            IsSuccess = false,
            Message = message,
            DurationMs = stopwatch.ElapsedMilliseconds
        };
    }
}
