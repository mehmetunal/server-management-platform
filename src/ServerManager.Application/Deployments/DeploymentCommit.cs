namespace ServerManager.Application.Deployments;

public sealed record DeploymentCommit(string Sha, string? Author, string? Subject);
