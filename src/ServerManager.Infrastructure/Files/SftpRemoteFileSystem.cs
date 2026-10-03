using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Renci.SshNet.Common;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Files;
using ServerManager.Application.Interfaces.Files;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Infrastructure.Files;

public sealed class SftpRemoteFileSystem : IRemoteFileSystem
{
    private const string HostKeyNotVerifiedMessage = "Host key henüz doğrulanmadı. Önce bağlantı testi yapın.";

    private readonly SshOptions _sshOptions;
    private readonly FileManagerOptions _fileOptions;
    private readonly IRemoteCommandRunner _commandRunner;
    private readonly ILogger<SftpRemoteFileSystem> _logger;

    public SftpRemoteFileSystem(
        IOptions<SshOptions> sshOptions,
        IOptions<FileManagerOptions> fileOptions,
        IRemoteCommandRunner commandRunner,
        ILogger<SftpRemoteFileSystem> logger)
    {
        _sshOptions = sshOptions.Value;
        _fileOptions = fileOptions.Value;
        _commandRunner = commandRunner;
        _logger = logger;
    }

    private TimeSpan OperationTimeout => TimeSpan.FromSeconds(Math.Max(5, _fileOptions.OperationTimeoutSeconds));

    public async Task<ServiceResult<T>> RunAsync<T>(
        RemoteExecutionContext context,
        Func<IRemoteFileSession, CancellationToken, Task<ServiceResult<T>>> work,
        CancellationToken cancellationToken = default)
    {
        var lease = await ConnectAsync<T>(context, cancellationToken);
        if (lease.Failure is not null)
            return lease.Failure;

        using (lease.Lease)
        {
            try
            {
                return await work(new SftpFileSession(lease.Lease!.Client), cancellationToken);
            }
            catch (RemoteFileException ex)
            {
                return ServiceResult<T>.Failure(ex.Detail ?? "Dosya işlemi başarısız oldu.");
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested
                                       && SshErrorTranslator.TryTranslate(ex, context.Connection, _logger, out var message))
            {
                return ServiceResult<T>.Failure(message);
            }
        }
    }

    public async Task<ServiceResult<RemoteFileStream>> OpenReadAsync(RemoteExecutionContext context, string path, CancellationToken cancellationToken = default)
    {
        var connected = await ConnectAsync<RemoteFileStream>(context, cancellationToken);
        if (connected.Failure is not null)
            return connected.Failure;

        var lease = connected.Lease!;
        try
        {
            var file = await lease.Client.GetAsync(path, cancellationToken);
            if (file.IsDirectory)
            {
                lease.Dispose();
                return ServiceResult<RemoteFileStream>.Failure("Klasörler indirilemez.", ServiceErrorType.Validation);
            }

            var stream = await lease.Client.OpenAsync(path, FileMode.Open, FileAccess.Read, cancellationToken);
            long? length = file.IsRegularFile ? file.Length : null;
            return ServiceResult<RemoteFileStream>.Success(new RemoteFileStream
            {
                Content = new LeasedStream(stream, lease),
                FileName = file.Name,
                Length = length
            });
        }
        catch (SftpPathNotFoundException)
        {
            lease.Dispose();
            return ServiceResult<RemoteFileStream>.NotFound("Dosya bulunamadı.");
        }
        catch (SftpPermissionDeniedException)
        {
            lease.Dispose();
            return ServiceResult<RemoteFileStream>.Failure("Sunucuda bu dosyayı okuma yetkisi yok (Permission denied).");
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested
                                   && SshErrorTranslator.TryTranslate(ex, context.Connection, _logger, out var message))
        {
            lease.Dispose();
            return ServiceResult<RemoteFileStream>.Failure(message);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    public Task<ServiceResult> CopyAsync(RemoteExecutionContext context, string source, string destination, CancellationToken cancellationToken = default) =>
        RunCommandAsync(context, RemoteFileCommands.Copy(source, destination), elevate: false, cancellationToken);

    public Task<ServiceResult> DeleteRecursiveAsync(RemoteExecutionContext context, string path, CancellationToken cancellationToken = default) =>
        RunCommandAsync(context, RemoteFileCommands.RemoveRecursive(path), elevate: false, cancellationToken);

    public Task<ServiceResult> ChangeModeAsync(RemoteExecutionContext context, string path, string mode, bool recursive, CancellationToken cancellationToken = default) =>
        RunCommandAsync(context, RemoteFileCommands.ChangeMode(path, mode, recursive), elevate: true, cancellationToken);

    public Task<ServiceResult> ChangeOwnerAsync(RemoteExecutionContext context, string path, string? owner, string? group, bool recursive, CancellationToken cancellationToken = default) =>
        RunCommandAsync(context, RemoteFileCommands.ChangeOwner(path, owner, group, recursive), elevate: true, cancellationToken);

    private async Task<ServiceResult> RunCommandAsync(RemoteExecutionContext context, string commandText, bool elevate, CancellationToken cancellationToken)
    {
        var result = await _commandRunner.RunAsync(context, async (executor, ct) =>
        {
            var output = await executor.ExecuteAsync(new RemoteCommand(commandText, OperationTimeout, elevate), ct);
            return output.IsSuccess
                ? ServiceResult<bool>.Success(true)
                : ServiceResult<bool>.Failure(DescribeFailure(output));
        }, cancellationToken);

        return result.IsSuccess ? ServiceResult.Success() : result;
    }

    private static string DescribeFailure(RemoteCommandOutput output)
    {
        if (output.TimedOut)
            return "İşlem zaman aşımına uğradı.";

        var line = output.Stderr
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;

        if (line.Contains("password is required", StringComparison.OrdinalIgnoreCase)
            || line.Contains("incorrect password", StringComparison.OrdinalIgnoreCase))
            return "sudo parolası gerekli veya hatalı. Sunucu ayarlarındaki sudo bilgisini kontrol edin.";
        if (line.Contains("Permission denied", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Operation not permitted", StringComparison.OrdinalIgnoreCase))
            return "Sunucuda bu işlem için yetki yok (Permission denied).";
        if (line.Contains("invalid user", StringComparison.OrdinalIgnoreCase)
            || line.Contains("invalid group", StringComparison.OrdinalIgnoreCase)
            || line.Contains("unknown user", StringComparison.OrdinalIgnoreCase)
            || line.Contains("unknown group", StringComparison.OrdinalIgnoreCase))
            return "Sunucuda böyle bir kullanıcı veya grup yok.";
        if (line.Contains("No such file", StringComparison.OrdinalIgnoreCase))
            return "Dosya veya klasör bulunamadı.";

        return line.Length == 0
            ? $"İşlem başarısız oldu (çıkış kodu {output.ExitCode})."
            : $"İşlem başarısız oldu: {(line.Length > 200 ? line[..200] : line)}";
    }

    private async Task<(SftpClientLease? Lease, ServiceResult<T>? Failure)> ConnectAsync<T>(RemoteExecutionContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(context.Connection.ExpectedHostKeyFingerprint))
            return (null, ServiceResult<T>.Failure(HostKeyNotVerifiedMessage));

        SftpClientLease lease;
        try
        {
            lease = SftpClientLease.Create(context.Connection, _sshOptions, OperationTimeout);
        }
        catch (Exception ex) when (ex is SshException or InvalidOperationException or ArgumentException)
        {
            _logger.LogWarning("SSH kimlik bilgisi hazırlanamadı. Target: {Target}, Error: {ErrorType}", context, ex.GetType().Name);
            return (null, ServiceResult<T>.Failure("Kimlik bilgisi okunamadı."));
        }

        try
        {
            await lease.ConnectAsync(_sshOptions, cancellationToken);
            return (lease, null);
        }
        catch (Exception ex) when (lease.HostKeyVerifier.Mismatch)
        {
            lease.Dispose();
            _logger.LogWarning("Host key fingerprint uyuşmuyor. Target: {Target}, Error: {ErrorType}", context, ex.GetType().Name);
            return (null, ServiceResult<T>.Failure(HostKeyVerifier.MismatchMessage));
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested
                                   && SshErrorTranslator.TryTranslate(ex, context.Connection, _logger, out var message))
        {
            lease.Dispose();
            if (ex is SshException { Message: var detail } && detail.Contains("subsystem", StringComparison.OrdinalIgnoreCase))
                return (null, ServiceResult<T>.Failure("Sunucuda SFTP alt sistemi etkin değil (openssh-sftp-server kurulu olmalı)."));
            return (null, ServiceResult<T>.Failure(message));
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }
}
