using ServerManager.Application.Commands;

namespace ServerManager.Application.Interfaces.Commands;

public interface IServerScriptExecutor
{
    /// <summary>Betiği sunucuda <c>sh -c</c> ile çalıştırır; stdout ve stderr birleştirilmiş döner.</summary>
    Task<ScriptExecutionResult> ExecuteAsync(Guid serverId, string script, bool elevate, TimeSpan timeout, CancellationToken cancellationToken = default);
}
