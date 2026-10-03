using ServerManager.Application.Dokploy;

namespace ServerManager.Application.DTOs.Dokploy;

public sealed class DokployCompatibilityReportDto
{
    public IReadOnlyList<DokployCompatibilityCheckDto> Checks { get; init; } = [];

    public bool IsRoot { get; init; }

    public bool BashAvailable { get; init; }

    public DateTime CheckedAt { get; init; }

    public bool CanInstall => Checks.All(c => c.Status != DokployCheckStatus.Failed);

    public bool HasWarnings => Checks.Any(c => c.Status == DokployCheckStatus.Warning);
}
