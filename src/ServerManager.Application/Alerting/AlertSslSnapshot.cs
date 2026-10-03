namespace ServerManager.Application.Alerting;

public sealed record AlertSslSnapshot(Guid Id, string Host, int Port, Guid? ServerId, string? ServerName, DateTime? NotAfter);
