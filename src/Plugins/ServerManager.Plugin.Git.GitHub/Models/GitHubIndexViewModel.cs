using ServerManager.Plugin.Git.GitHub.DTOs;

namespace ServerManager.Plugin.Git.GitHub.Models;

public sealed class GitHubIndexViewModel
{
    public IReadOnlyList<GitHubAppDto> Apps { get; init; } = [];

    public string DefaultAppName { get; init; } = string.Empty;

    /// <summary>GitHub'ın dönüş yapacağı panel adresi.</summary>
    public string PanelBaseUrl { get; init; } = string.Empty;

    public string WebUrl { get; init; } = string.Empty;

    public string? Notice { get; init; }

    public bool NoticeIsError { get; init; }
}
