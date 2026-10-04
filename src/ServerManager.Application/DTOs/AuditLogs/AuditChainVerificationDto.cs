namespace ServerManager.Application.DTOs.AuditLogs;

public sealed class AuditChainVerificationDto
{
    public bool IsValid { get; init; }

    public int CheckedCount { get; init; }

    public int SignedCount { get; init; }

    /// <summary>Zincir özelliğinden önce yazılmış (imzasız) kayıtlar.</summary>
    public int UnsignedCount { get; init; }

    public long? BrokenAtId { get; init; }

    public DateTime? BrokenAtTime { get; init; }

    public string Message { get; init; } = string.Empty;
}
