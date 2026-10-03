namespace ServerManager.Application.DTOs.Docker;

public sealed class DockerLogQuery
{
    public string Container { get; set; } = string.Empty;

    public int? Tail { get; set; }

    /// <summary>RFC3339 (nanosaniye dahil) zaman damgası; yalnızca bu andan sonraki satırlar döner.</summary>
    public string? Since { get; set; }
}
