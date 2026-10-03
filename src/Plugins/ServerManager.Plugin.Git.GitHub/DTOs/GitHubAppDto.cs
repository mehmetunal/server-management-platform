namespace ServerManager.Plugin.Git.GitHub.DTOs;

public sealed class GitHubAppDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public long AppId { get; init; }

    public string Slug { get; init; } = string.Empty;

    public string? OwnerLogin { get; init; }

    public string? HtmlUrl { get; init; }

    public string InstallUrl { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public string? CreatedBy { get; init; }

    public IReadOnlyList<GitHubInstallationDto> Installations { get; init; } = [];

    /// <summary>Kurulumlar GitHub'dan alınamadıysa nedeni.</summary>
    public string? InstallationsError { get; init; }

    public IReadOnlyList<string> ProjectNames { get; init; } = [];
}
