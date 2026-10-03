using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet;
using Renci.SshNet.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Ssh;

namespace ServerManager.Infrastructure.Ssh;

public sealed class SshNetConnectionTester : ISshConnectionTester
{
    private const string OsReleaseCommand = "cat /etc/os-release 2>/dev/null || uname -sr";

    private readonly SshOptions _options;
    private readonly ILogger<SshNetConnectionTester> _logger;

    public SshNetConnectionTester(IOptions<SshOptions> options, ILogger<SshNetConnectionTester> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SshConnectionTestResult> TestAsync(SshConnectionRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        AuthenticationMethod authenticationMethod;
        try
        {
            authenticationMethod = SshAuthenticationFactory.Create(request);
        }
        catch (Exception ex) when (ex is SshException or InvalidOperationException or ArgumentException)
        {
            _logger.LogWarning("SSH kimlik bilgisi hazırlanamadı. Target: {Target}, Error: {ErrorType}", request, ex.GetType().Name);
            return Failure("Private key okunamadı. Anahtar formatını veya passphrase'i kontrol edin.", stopwatch);
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
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.ConnectionTimeoutSeconds + 5));

            await client.ConnectAsync(timeoutCts.Token);
            var operatingSystem = await TryReadOperatingSystemAsync(client, timeoutCts.Token);
            client.Disconnect();

            stopwatch.Stop();
            return new SshConnectionTestResult
            {
                IsSuccess = true,
                Message = $"Bağlantı başarılı ({stopwatch.ElapsedMilliseconds} ms).",
                HostKeyFingerprint = hostKeyVerifier.ReceivedFingerprint,
                OperatingSystem = operatingSystem,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex) when (hostKeyVerifier.Mismatch)
        {
            _logger.LogWarning("Host key fingerprint uyuşmuyor. Target: {Target}, Error: {ErrorType}", request, ex.GetType().Name);
            return new SshConnectionTestResult
            {
                IsSuccess = false,
                FingerprintMismatch = true,
                HostKeyFingerprint = hostKeyVerifier.ReceivedFingerprint,
                Message = HostKeyVerifier.MismatchMessage,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested
                                   && SshErrorTranslator.TryTranslate(ex, request, _logger, out var message))
        {
            return Failure(message, stopwatch, hostKeyVerifier.ReceivedFingerprint);
        }
    }

    internal static string? ParseOperatingSystem(string? output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("PRETTY_NAME=", StringComparison.Ordinal))
                return line["PRETTY_NAME=".Length..].Trim('"');
        }

        var firstLine = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstLine) ? null : firstLine;
    }

    private async Task<string?> TryReadOperatingSystemAsync(SshClient client, CancellationToken cancellationToken)
    {
        try
        {
            using var command = client.CreateCommand(OsReleaseCommand);
            command.CommandTimeout = TimeSpan.FromSeconds(_options.CommandTimeoutSeconds);
            await command.ExecuteAsync(cancellationToken);
            return ParseOperatingSystem(command.Result);
        }
        catch (Exception ex) when (ex is SshException or OperationCanceledException)
        {
            _logger.LogInformation("İşletim sistemi bilgisi okunamadı: {ErrorType}", ex.GetType().Name);
            return null;
        }
    }

    private static SshConnectionTestResult Failure(string message, Stopwatch stopwatch, string? fingerprint = null)
    {
        stopwatch.Stop();
        return new SshConnectionTestResult
        {
            IsSuccess = false,
            Message = message,
            HostKeyFingerprint = fingerprint,
            DurationMs = stopwatch.ElapsedMilliseconds
        };
    }
}
