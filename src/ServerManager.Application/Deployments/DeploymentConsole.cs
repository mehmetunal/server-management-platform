namespace ServerManager.Application.Deployments;

/// <summary>Deployment loguna yazılan panel satırları (ANSI renkli, CRLF ile); komut çıktısından ayırt edilir.</summary>
public static class DeploymentConsole
{
    public static string Step(string message) => $"\u001b[1;35m==> {message}\u001b[0m\r\n";

    public static string Info(string message) => $"\u001b[36m--> {message}\u001b[0m\r\n";

    public static string Success(string message) => $"\u001b[32m==> {message}\u001b[0m\r\n";

    public static string Error(string message) => $"\u001b[31m==> {message}\u001b[0m\r\n";

    /// <summary>xterm satır başına dönebilsin diye yalnız LF'ler CRLF'e çevrilir.</summary>
    public static string NormalizeNewLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
}
