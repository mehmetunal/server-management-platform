namespace ServerManager.Application.Security;

public sealed class SecurityReport
{
    public int Score { get; init; }

    public int CriticalCount { get; init; }

    public int WarningCount { get; init; }

    public IReadOnlyList<SecurityFinding> Findings { get; init; } = [];

    public SecurityFacts Facts { get; init; } = new();
}
