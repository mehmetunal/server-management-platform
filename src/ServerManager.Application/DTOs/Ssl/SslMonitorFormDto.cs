namespace ServerManager.Application.DTOs.Ssl;

public sealed class SslMonitorFormDto
{
    public Guid? Id { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 443;

    public Guid? ServerId { get; set; }

    public bool IsEnabled { get; set; } = true;
}
