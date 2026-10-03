namespace ServerManager.Application.DTOs.Monitoring;

public sealed class SystemInfo
{
    public string? Hostname { get; set; }

    public string? OperatingSystem { get; set; }

    public string? Kernel { get; set; }

    public string? Architecture { get; set; }

    public string? Timezone { get; set; }

    public long UptimeSeconds { get; set; }

    public DateTime? BootTimeUtc { get; set; }
}
