namespace ServerManager.Plugin.Git.GitHub.Models;

public sealed class GitHubReturnViewModel
{
    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    /// <summary>Panel içi göreli adres.</summary>
    public string TargetUrl { get; init; } = string.Empty;
}
