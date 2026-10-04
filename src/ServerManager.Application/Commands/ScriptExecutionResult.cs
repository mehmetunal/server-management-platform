namespace ServerManager.Application.Commands;

/// <param name="ErrorMessage">Komut hiç çalıştırılamadıysa (bağlantı, kimlik doğrulama) neden; çalıştıysa null.</param>
public sealed record ScriptExecutionResult(bool Executed, int? ExitCode, bool TimedOut, string Output, string? ErrorMessage)
{
    public bool IsSuccess => Executed && !TimedOut && ExitCode == 0;
}
