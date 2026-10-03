namespace ServerManager.Application.DTOs.Deployments;

public sealed class DeploymentRunResult
{
    public bool Succeeded { get; init; }

    public string? FailureReason { get; init; }

    public int? ExitCode { get; init; }

    public string? CommitSha { get; init; }

    public string? CommitMessage { get; init; }

    public string? CommitAuthor { get; init; }
}
