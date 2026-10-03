namespace ServerManager.Plugin.Git.GitHub.DTOs;

public sealed class GitHubInstallationDto
{
    public long Id { get; init; }

    public string Account { get; init; } = string.Empty;

    public bool IsOrganization { get; init; }

    public bool AllRepositories { get; init; }

    public bool IsSuspended { get; init; }

    public string? HtmlUrl { get; init; }
}
