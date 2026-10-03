namespace ServerManager.Plugin.Git.GitHub.DTOs;

public sealed record GitHubRepositoryInfo(string FullName, string CloneUrl, string DefaultBranch, bool IsPrivate, string? HtmlUrl);
