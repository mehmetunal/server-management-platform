using ServerManager.Domain.Common;
using ServerManager.Domain.Enums;

namespace ServerManager.Domain.Entities;

public class SslCertificateMonitor : BaseEntity
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 443;

    public Guid? ServerId { get; set; }

    public bool IsEnabled { get; set; } = true;

    public SslCertificateStatus Status { get; set; } = SslCertificateStatus.Unknown;

    public string? Subject { get; set; }

    public string? Issuer { get; set; }

    public DateTime? NotBefore { get; set; }

    public DateTime? NotAfter { get; set; }

    public string? ResolvedAddress { get; set; }

    public DateTime? LastCheckedAt { get; set; }

    public string? LastError { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    public Server? Server { get; set; }
}
