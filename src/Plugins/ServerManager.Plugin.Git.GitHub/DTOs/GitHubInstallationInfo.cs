namespace ServerManager.Plugin.Git.GitHub.DTOs;

/// <param name="RepositorySelection"><c>all</c> veya <c>selected</c>.</param>
public sealed record GitHubInstallationInfo(
    long Id,
    string AccountLogin,
    string AccountType,
    string RepositorySelection,
    string? HtmlUrl,
    bool IsSuspended);
