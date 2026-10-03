namespace ServerManager.Plugin.DevOps.Dokploy.Core;

/// <summary>Kurulum terminaline yazılan panel satırları (ANSI renkli, CRLF ile).</summary>
public static class DokployConsole
{
    public static string Info(string message) => $"\u001b[36m==> {message}\u001b[0m\r\n";

    public static string Success(string message) => $"\u001b[32m==> {message}\u001b[0m\r\n";

    public static string Error(string message) => $"\u001b[31m==> {message}\u001b[0m\r\n";
}
