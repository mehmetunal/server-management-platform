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

        try
        {
            // stdin yazımı da zaman aşımına tabidir; iptal çağıranın değilse zaman aşımı sonucu döner, istisna dışarı sızmaz.
            await WriteInputAsync(sshCommand, command, timeoutCts.Token);
            await executeTask;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Observe(executeTask);
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

        using var gate = new SemaphoreSlim(1, 1);
        var stdout = new OutputTail();
        var stderr = new OutputTail();
        var pumps = Task.WhenAll(
            PumpAsync(sshCommand.OutputStream, stdout, onOutput, gate, timeoutCts.Token),
            PumpAsync(sshCommand.ExtendedOutputStream, stderr, onOutput, gate, timeoutCts.Token));

        var timedOut = false;
        try
        {
            await WriteInputAsync(sshCommand, command, timeoutCts.Token);
            await executeTask;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Observe(executeTask);
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

    public async Task<RemoteCommandOutput> ExecuteWithOutputStreamAsync(
        RemoteCommand command,
        Func<Stream, CancellationToken, Task> consumeOutput,
        CancellationToken cancellationToken = default)
    {
        var commandText = SudoCommandBuilder.Build(_context, command.CommandText, command.Elevate);
        using var sshCommand = _client.CreateCommand(commandText);
        sshCommand.CommandTimeout = command.Timeout;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(command.Timeout);
        using var abortCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);

        var executeTask = sshCommand.ExecuteAsync(abortCts.Token);

        using var gate = new SemaphoreSlim(1, 1);
        var stderr = new OutputTail();
        var stderrPump = PumpAsync(sshCommand.ExtendedOutputStream, stderr, static (_, _) => Task.CompletedTask, gate, abortCts.Token);

        Exception? consumeError = null;
        var inputTimedOut = false;
        try
        {
            await WriteInputAsync(sshCommand, command, abortCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Zaman aşımı: aşağıdaki WaitAsync TimedOut sonucunu üretir.
            inputTimedOut = true;
        }

        if (!inputTimedOut)
        {
            try
            {
                await consumeOutput(sshCommand.OutputStream, abortCts.Token);
            }
            catch (Exception ex)
            {
                consumeError = ex;
                await abortCts.CancelAsync();
            }
        }

        var (exitCode, timedOut) = await WaitAsync(sshCommand, executeTask, cancellationToken, timeoutCts.Token);
        await Task.WhenAny(stderrPump, Task.Delay(StreamDrainTimeout, CancellationToken.None));

        if (consumeError is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(consumeError);

        return new RemoteCommandOutput { ExitCode = exitCode, TimedOut = timedOut, Stderr = stderr.ToString() };
    }

    public async Task<RemoteCommandOutput> ExecuteWithInputStreamAsync(
        RemoteCommand command,
        Func<Stream, CancellationToken, Task> produceInput,
        CancellationToken cancellationToken = default)
    {
        var commandText = SudoCommandBuilder.Build(_context, command.CommandText, command.Elevate);
        using var sshCommand = _client.CreateCommand(commandText);
        sshCommand.CommandTimeout = command.Timeout;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(command.Timeout);
        using var abortCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token);

        var executeTask = sshCommand.ExecuteAsync(abortCts.Token);

        using var gate = new SemaphoreSlim(1, 1);
        var stdout = new OutputTail();
        var stderr = new OutputTail();
        var pumps = Task.WhenAll(
            PumpAsync(sshCommand.OutputStream, stdout, static (_, _) => Task.CompletedTask, gate, abortCts.Token),
            PumpAsync(sshCommand.ExtendedOutputStream, stderr, static (_, _) => Task.CompletedTask, gate, abortCts.Token));

        Exception? produceError = null;
        // Girdi akışı kapatılınca uzak komut EOF görür; hata olursa komut da durdurulur ki yarım veri işlenmesin.
        await using (var input = sshCommand.CreateInputStream())
        {
            try
            {
                if (SudoCommandBuilder.RequiresPasswordInput(_context, command.Elevate))
                    await input.WriteAsync(Encoding.UTF8.GetBytes(_context.SudoPassword + "\n"), abortCts.Token);
                if (!string.IsNullOrEmpty(command.StandardInput))
                    await input.WriteAsync(Encoding.UTF8.GetBytes(command.StandardInput), abortCts.Token);

                await produceInput(input, abortCts.Token);
            }
            catch (Exception ex)
            {
                produceError = ex;
                await abortCts.CancelAsync();
            }
        }

        var (exitCode, timedOut) = await WaitAsync(sshCommand, executeTask, cancellationToken, timeoutCts.Token);
        await Task.WhenAny(pumps, Task.Delay(StreamDrainTimeout, CancellationToken.None));

        if (produceError is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(produceError);

        return new RemoteCommandOutput { ExitCode = exitCode, TimedOut = timedOut, Stdout = stdout.ToString(), Stderr = stderr.ToString() };
    }

    private static async Task<(int? ExitCode, bool TimedOut)> WaitAsync(
        SshCommand sshCommand,
        Task executeTask,
        CancellationToken cancellationToken,
        CancellationToken timeoutToken)
    {
        try
        {
            await executeTask;
            return (sshCommand.ExitStatus, false);
        }
        catch (OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return (null, timeoutToken.IsCancellationRequested);
        }
        catch (SshOperationTimeoutException)
        {
            return (null, true);
        }
    }

    /// <summary>Beklenmeden bırakılan komut görevinin hatası gözlemlenir; gözlemsiz görev istisnası oluşmaz.</summary>
    private static void Observe(Task task) =>
        _ = task.ContinueWith(static t => _ = t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);

    private async Task WriteInputAsync(SshCommand sshCommand, RemoteCommand command, CancellationToken cancellationToken)
    {
        // stdin her durumda kapatılır; aksi halde stdin okuyan komutlar sonsuza kadar bekler.
        await using var input = sshCommand.CreateInputStream();
        if (SudoCommandBuilder.RequiresPasswordInput(_context, command.Elevate))
        {
            await input.WriteAsync(Encoding.UTF8.GetBytes(_context.SudoPassword + "\n"), cancellationToken);
        }

        if (!string.IsNullOrEmpty(command.StandardInput))
        {
            await input.WriteAsync(Encoding.UTF8.GetBytes(command.StandardInput), cancellationToken);
        }
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
