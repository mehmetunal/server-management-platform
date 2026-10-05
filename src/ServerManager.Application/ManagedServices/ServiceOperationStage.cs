namespace ServerManager.Application.ManagedServices;

/// <summary>İşlem aşamaları; adım göstergesi bu adları kullanır.</summary>
public enum ServiceOperationStage
{
    Docker = 1,
    Ports = 2,
    Pull = 3,
    Network = 4,
    Start = 5,
    Health = 6,
    Test = 7,
    Stop = 8,
    Cleanup = 9,
    Completed = 10,
    Failed = 11
}

public static class ServiceOperationStages
{
    public static readonly IReadOnlyList<(ServiceOperationStage Stage, string Label)> Install =
    [
        (ServiceOperationStage.Docker, "Docker"),
        (ServiceOperationStage.Ports, "Port kontrolü"),
        (ServiceOperationStage.Pull, "İmaj"),
        (ServiceOperationStage.Network, "Ağ ve volume"),
        (ServiceOperationStage.Start, "Container"),
        (ServiceOperationStage.Health, "Sağlık"),
        (ServiceOperationStage.Test, "Bağlantı testi"),
        (ServiceOperationStage.Completed, "Tamamlandı")
    ];

    public static readonly IReadOnlyList<(ServiceOperationStage Stage, string Label)> Remove =
    [
        (ServiceOperationStage.Docker, "Docker"),
        (ServiceOperationStage.Stop, "Container"),
        (ServiceOperationStage.Cleanup, "Temizlik"),
        (ServiceOperationStage.Completed, "Tamamlandı")
    ];
}

/// <summary>İşlem loguna yazılan panel satırları (ANSI renkli, CRLF ile); komut çıktısından ayırt edilir.</summary>
public static class ServiceConsole
{
    public static string Step(string message) => $"\u001b[1;35m==> {message}\u001b[0m\r\n";

    public static string Info(string message) => $"\u001b[36m--> {message}\u001b[0m\r\n";

    public static string Warning(string message) => $"\u001b[33m--> {message}\u001b[0m\r\n";

    public static string Success(string message) => $"\u001b[32m==> {message}\u001b[0m\r\n";

    public static string Error(string message) => $"\u001b[31m==> {message}\u001b[0m\r\n";

    /// <summary>xterm satır başına dönebilsin diye yalnız LF'ler CRLF'e çevrilir.</summary>
    public static string NormalizeNewLines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);
}
