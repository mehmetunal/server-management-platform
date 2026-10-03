namespace ServerManager.Application.DTOs.Ssh;

public sealed class TerminalOpenRequest
{
    public required RemoteExecutionContext Context { get; init; }

    /// <summary>Etkileşimli PTY içinde login shell'in yerine çalıştırılacak komut (exec ile).</summary>
    public required string Command { get; init; }

    /// <summary>Sunucuda sudo açıksa komut sudo ile çalıştırılır.</summary>
    public bool Elevate { get; init; }

    public int Columns { get; init; } = 120;

    public int Rows { get; init; } = 32;
}
