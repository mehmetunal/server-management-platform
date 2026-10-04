using System.Text;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Backups;
using ServerManager.Application.Interfaces.Ssh;

namespace ServerManager.Infrastructure.Backups;

public sealed class SshBackupSourceRunner : IBackupSourceRunner
{
    private readonly IRemoteCommandRunner _runner;

    public SshBackupSourceRunner(IRemoteCommandRunner runner)
    {
        _runner = runner;
    }

    public Task<ServiceResult> ExportAsync(
        ServerConnection connection,
        BackupSourceSpec source,
        Func<Stream, CancellationToken, Task> consume,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) =>
        RunAsync(connection, async (executor, ct) =>
        {
            var command = new RemoteCommand(
                BackupCommands.Export(source),
                timeout,
                Elevate: true,
                StandardInput: BackupCommands.NeedsPasswordInput(source) ? BackupCommands.PasswordInput(source) : null);

            var output = await executor.ExecuteWithOutputStreamAsync(command, consume, ct);
            return output.IsSuccess
                ? ServiceResult.Success()
                : ServiceResult.Failure(BackupErrorTranslator.Translate(output, "Yedekleme"));
        }, cancellationToken);

    public Task<ServiceResult> ImportAsync(
        ServerConnection connection,
        BackupSourceSpec target,
        Func<Stream, CancellationToken, Task> produce,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) =>
        RunAsync(connection, async (executor, ct) =>
        {
            var command = new RemoteCommand(BackupCommands.Import(target), timeout, Elevate: true);
            var needsPassword = BackupCommands.NeedsPasswordInput(target);

            var output = await executor.ExecuteWithInputStreamAsync(command, async (input, token) =>
            {
                if (needsPassword)
                    await input.WriteAsync(Encoding.UTF8.GetBytes(BackupCommands.PasswordInput(target)), token);
                await produce(input, token);
            }, ct);

            return output.IsSuccess
                ? ServiceResult.Success()
                : ServiceResult.Failure(BackupErrorTranslator.Translate(output, "Geri yükleme"));
        }, cancellationToken);

    private async Task<ServiceResult> RunAsync(
        ServerConnection connection,
        Func<IRemoteCommandExecutor, CancellationToken, Task<ServiceResult>> work,
        CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync<bool>(connection.Context, async (executor, ct) =>
        {
            var inner = await work(executor, ct);
            return inner.IsSuccess ? ServiceResult<bool>.Success(true) : ServiceResult<bool>.Failure(inner.Message ?? "İşlem başarısız.");
        }, cancellationToken);

        return result.IsSuccess ? ServiceResult.Success() : ServiceResult.Failure(result.Message ?? "İşlem başarısız.");
    }
}
