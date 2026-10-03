using System.Diagnostics;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet;
using Renci.SshNet.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Domain.Enums;

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
        string? receivedFingerprint = null;
        var fingerprintMismatch = false;

        AuthenticationMethod authenticationMethod;
        try
        {
            authenticationMethod = CreateAuthenticationMethod(request);
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
        client.HostKeyReceived += (_, e) =>
        {
            receivedFingerprint = ComputeSha256Fingerprint(e.HostKey);
            if (request.ExpectedHostKeyFingerprint is not null
                && !string.Equals(request.ExpectedHostKeyFingerprint, receivedFingerprint, StringComparison.Ordinal))
            {
                fingerprintMismatch = true;
                e.CanTrust = false;
                return;
            }

            e.CanTrust = true;
        };

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
                HostKeyFingerprint = receivedFingerprint,
                OperatingSystem = operatingSystem,
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex) when (fingerprintMismatch)
        {
            _logger.LogWarning("Host key fingerprint uyuşmuyor. Target: {Target}, Error: {ErrorType}", request, ex.GetType().Name);
            return new SshConnectionTestResult
            {
                IsSuccess = false,
                FingerprintMismatch = true,
                HostKeyFingerprint = receivedFingerprint,
                Message = "Host key fingerprint kayıtlı değerle uyuşmuyor. Sunucu değişmiş veya bağlantı araya giren bir saldırıya maruz kalıyor olabilir.",
                DurationMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (SshAuthenticationException)
        {
            return Failure("Kimlik doğrulama başarısız. Kullanıcı adı, parola veya anahtarı kontrol edin.", stopwatch, receivedFingerprint);
        }
        catch (SshOperationTimeoutException)
        {
            return Failure("Bağlantı zaman aşımına uğradı.", stopwatch, receivedFingerprint);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure("Bağlantı zaman aşımına uğradı.", stopwatch, receivedFingerprint);
        }
        catch (SocketException ex)
        {
            _logger.LogInformation("SSH soket hatası. Target: {Target}, SocketError: {SocketError}", request, ex.SocketErrorCode);
            return Failure($"Sunucuya ulaşılamadı ({ex.SocketErrorCode}).", stopwatch, receivedFingerprint);
        }
        catch (SshConnectionException ex)
        {
            _logger.LogInformation("SSH bağlantı hatası. Target: {Target}, Reason: {Reason}", request, ex.DisconnectReason);
            return Failure("SSH bağlantısı kurulamadı.", stopwatch, receivedFingerprint);
        }
        catch (SshException ex)
        {
            _logger.LogWarning("SSH hatası. Target: {Target}, Error: {ErrorType}", request, ex.GetType().Name);
            return Failure("SSH bağlantısı sırasında bir hata oluştu.", stopwatch, receivedFingerprint);
        }
    }

    internal static string ComputeSha256Fingerprint(byte[] hostKey)
    {
        var hash = SHA256.HashData(hostKey);
        return "SHA256:" + Convert.ToBase64String(hash).TrimEnd('=');
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

    private static AuthenticationMethod CreateAuthenticationMethod(SshConnectionRequest request)
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
