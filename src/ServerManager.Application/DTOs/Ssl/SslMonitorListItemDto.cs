using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Ssl;

public sealed record SslMonitorListItemDto(
    Guid Id,
    string Host,
    int Port,
    Guid? ServerId,
    string? ServerName,
    bool IsEnabled,
    SslCertificateStatus Status,
    string? Subject,
    string? Issuer,
    DateTime? NotBefore,
    DateTime? NotAfter,
    int? DaysRemaining,
    string? ResolvedAddress,
    DateTime? LastCheckedAt,
    string? LastError);
