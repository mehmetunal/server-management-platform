namespace ServerManager.Plugin.Git.GitHub;

public static class GitHubPlugin
{
    /// <summary>plugin.json içindeki SystemName ile aynı olmalıdır; projelerde entegrasyon adı olarak saklanır.</summary>
    public const string SystemName = "Git.GitHub";

    public const string DisplayName = "GitHub App";

    public const string RateLimitPolicy = "github-action";

    public const string AuditEntityType = "GitHubApp";
}
