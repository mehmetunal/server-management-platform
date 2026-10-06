using ServerManager.Domain.Enums;

namespace ServerManager.Application.Alerting;

/// <summary>Kaynak geçmişi toplayıcısının yazdığı container durum örneği.</summary>
public sealed record AlertContainerSample(Guid ServerId, string ContainerName, DateTime CollectedAt, string State, string? Health, int RestartCount)
{
    public bool IsRunning => string.Equals(State, "running", StringComparison.OrdinalIgnoreCase);

    public bool IsUnhealthy => string.Equals(Health, "unhealthy", StringComparison.OrdinalIgnoreCase);

    /// <summary>Çalışıyor ve sağlıksız değil (healthcheck yoksa veya "starting" ise sağlıklı sayılır).</summary>
    public bool IsUp => IsRunning && !IsUnhealthy;
}

/// <summary>Kaldırılmamış yönetilen servis ve sunucusu.</summary>
public sealed record AlertManagedServiceSnapshot(
    Guid ServiceId,
    string Name,
    string ContainerName,
    ManagedServiceStatus Status,
    Guid ServerId,
    string ServerName,
    ServerStatus ServerStatus);

/// <summary>Sunucunun son temizlik taraması özeti; hiç taranmadıysa <see cref="ScannedAt"/> null.</summary>
public sealed record AlertReclaimableSnapshot(
    Guid ServerId,
    string ServerName,
    ServerStatus ServerStatus,
    DateTime? ScannedAt,
    long ReclaimableBytes,
    long SafeReclaimableBytes);
