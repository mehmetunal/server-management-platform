using ServerManager.Application.Commands;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Commands;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Infrastructure.Ssh;

namespace ServerManager.Infrastructure.Commands;

public sealed class SshServerScriptExecutor : IServerScriptExecutor
{
    private readonly IServerConnectionProvider _connectionProvider;
    private readonly IRemoteCommandRunner _runner;

    public SshServerScriptExecutor(IServerConnectionProvider connectionProvider, IRemoteCommandRunner runner)
    {
        _connectionProvider = connectionProvider;
        _runner = runner;
    }

    public async Task<ScriptExecutionResult> ExecuteAsync(Guid serverId, string script, bool elevate, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var connection = await _connectionProvider.GetAsync(serverId, cancellationToken);
        if (!connection.IsSuccess || connection.Data is null)
            return NotExecuted(connection.Message ?? "Sunucu bağlantı bilgisi alınamadı.");

        if (elevate && !connection.Data.Context.UseSudo)
            return NotExecuted("Bu sunucuda sudo kullanımı açık değil; komut yetkili çalıştırılmadı.");

        var command = new RemoteCommand(BuildCommand(script), timeout, Elevate: elevate);
        var result = await _runner.RunAsync(connection.Data.Context, async (executor, ct) =>
        {
            var output = await executor.ExecuteAsync(command, ct);
            return ServiceResult<RemoteCommandOutput>.Success(output);
        }, cancellationToken);

        if (!result.IsSuccess || result.Data is null)
            return NotExecuted(result.Message ?? "Sunucuya bağlanılamadı.");

        var data = result.Data;
        return new ScriptExecutionResult(true, data.ExitCode, data.TimedOut, data.Stdout, null);
    }

    /// <summary>stderr stdout'a yönlendirilir; böylece çıktı sunucuda yazıldığı sırayla saklanır.</summary>
    public static string BuildCommand(string script) =>
        "sh -c " + ShellQuote.Quote(script.Replace("\r\n", "\n") + "\n") + " 2>&1";

    private static ScriptExecutionResult NotExecuted(string message) => new(false, null, false, string.Empty, message);
}
