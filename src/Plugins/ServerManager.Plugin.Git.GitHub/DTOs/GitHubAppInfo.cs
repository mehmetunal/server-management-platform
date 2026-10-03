namespace ServerManager.Plugin.Git.GitHub.DTOs;

public sealed record GitHubAppInfo(long Id, string Slug, string Name, string? OwnerLogin, string? HtmlUrl);
