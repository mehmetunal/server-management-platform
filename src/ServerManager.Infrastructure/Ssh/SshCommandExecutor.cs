using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Ssh;

namespace ServerManager.Infrastructure.Ssh;

internal sealed class SshCommandExecutor : IRemoteCommandExecutor
{
    public const int MaxOutputChars = 4_000_000;

    private readonly SshClient _client;
    private readonly RemoteExecutionContext _context;

    public SshCommandExecutor(SshClient client, RemoteExecutionContext context)
    {
        _client = client;
        _context = context;
    }

    public async Task<RemoteCommandOutput> ExecuteAsync(RemoteCommand command, CancellationToken cancellationToken = default)
    {
        var commandText = SudoCommandBuilder.Build(_context, command.CommandText, command.Elevate);
        using var sshCommand = _client.CreateCommand(commandText);
        sshCommand.CommandTimeout = command.Timeout;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(command.Timeout);

        var executeTask = sshCommand.ExecuteAsync(timeoutCts.Token);

        // stdin her durumda kapatılır; aksi halde stdin okuyan komutlar sonsuza kadar bekler.
        await using (var input = sshCommand.CreateInputStream())
        {
            if (SudoCommandBuilder.RequiresPasswordInput(_context, command.Elevate))
            {
                await input.WriteAsync(Encoding.UTF8.GetBytes(_context.SudoPassword + "\n"), timeoutCts.Token);
            }
        }

        try
        {
            await executeTask;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new RemoteCommandOutput { TimedOut = true, Stdout = Limit(sshCommand.Result), Stderr = Limit(sshCommand.Error) };
        }
        catch (SshOperationTimeoutException)
        {
            return new RemoteCommandOutput { TimedOut = true };
        }

        return new RemoteCommandOutput
        {
            ExitCode = sshCommand.ExitStatus,
            Stdout = Limit(sshCommand.Result),
            Stderr = Limit(sshCommand.Error)
        };
    }

    private static string Limit(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Length <= MaxOutputChars ? value : value[^MaxOutputChars..];
    }
}
