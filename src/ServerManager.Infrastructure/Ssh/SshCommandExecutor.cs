using System.Text;
using Renci.SshNet;
using Renci.SshNet.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Ssh;

namespace ServerManager.Infrastructure.Ssh;

internal sealed class SshCommandExecutor : IRemoteCommandExecutor
{
    public const int MaxOutputChars = 4_000_000;

    private static readonly TimeSpan StreamDrainTimeout = TimeSpan.FromSeconds(3);

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

    public async Task<RemoteCommandOutput> ExecuteStreamingAsync(
        RemoteCommand command,
        Func<string, CancellationToken, Task> onOutput,
        CancellationToken cancellationToken = default)
    {
        var commandText = SudoCommandBuilder.Build(_context, command.CommandText, command.Elevate);
        using var sshCommand = _client.CreateCommand(commandText);
        sshCommand.CommandTimeout = command.Timeout;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(command.Timeout);

        var executeTask = sshCommand.ExecuteAsync(timeoutCts.Token);

        await using (var input = sshCommand.CreateInputStream())
        {
            if (SudoCommandBuilder.RequiresPasswordInput(_context, command.Elevate))
            {
                await input.WriteAsync(Encoding.UTF8.GetBytes(_context.SudoPassword + "\n"), timeoutCts.Token);
            }
        }

        using var gate = new SemaphoreSlim(1, 1);
        var stdout = new OutputTail();
        var stderr = new OutputTail();
        var pumps = Task.WhenAll(
            PumpAsync(sshCommand.OutputStream, stdout, onOutput, gate, timeoutCts.Token),
            PumpAsync(sshCommand.ExtendedOutputStream, stderr, onOutput, gate, timeoutCts.Token));

        var timedOut = false;
        try
        {
            await executeTask;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
        }
        catch (SshOperationTimeoutException)
        {
            timedOut = true;
        }

        // Akışlar komut bitince EOF döner; kanal beklenmedik şekilde açık kalırsa sonsuza dek beklenmez.
        await Task.WhenAny(pumps, Task.Delay(StreamDrainTimeout, CancellationToken.None));

        return new RemoteCommandOutput
        {
            ExitCode = timedOut ? null : sshCommand.ExitStatus,
            TimedOut = timedOut,
            Stdout = stdout.ToString(),
            Stderr = stderr.ToString()
        };
    }

    private static async Task PumpAsync(
        Stream stream,
        OutputTail tail,
        Func<string, CancellationToken, Task> onOutput,
        SemaphoreSlim gate,
        CancellationToken cancellationToken)
    {
        var decoder = Encoding.UTF8.GetDecoder();
        var bytes = new byte[8192];
        var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
        try
        {
            int read;
            while ((read = await stream.ReadAsync(bytes, cancellationToken)) > 0)
            {
                var count = decoder.GetChars(bytes, 0, read, chars, 0, flush: false);
                if (count == 0)
                    continue;

                var text = new string(chars, 0, count);
                tail.Append(text);
                await gate.WaitAsync(cancellationToken);
                try
                {
                    await onOutput(text, cancellationToken);
                }
                finally
                {
                    gate.Release();
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static string Limit(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Length <= MaxOutputChars ? value : value[^MaxOutputChars..];
    }
}
