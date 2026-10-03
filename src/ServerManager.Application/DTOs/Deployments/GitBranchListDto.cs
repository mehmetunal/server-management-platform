namespace ServerManager.Application.DTOs.Deployments;

public sealed class GitBranchListDto
{
    public IReadOnlyList<string> Branches { get; init; } = [];

    public string ConfiguredBranch { get; init; } = string.Empty;

    public bool ConfiguredBranchExists { get; init; }
}
