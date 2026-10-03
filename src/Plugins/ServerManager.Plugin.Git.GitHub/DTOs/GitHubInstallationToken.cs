namespace ServerManager.Plugin.Git.GitHub.DTOs;

public sealed record GitHubInstallationToken(string Token, DateTimeOffset ExpiresAt);
