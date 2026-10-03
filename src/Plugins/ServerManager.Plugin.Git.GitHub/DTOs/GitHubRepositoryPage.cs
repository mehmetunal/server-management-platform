namespace ServerManager.Plugin.Git.GitHub.DTOs;

public sealed record GitHubRepositoryPage(IReadOnlyList<GitHubRepositoryInfo> Repositories, int TotalCount);
