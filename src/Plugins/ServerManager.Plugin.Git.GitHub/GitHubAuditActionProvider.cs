using ServerManager.Application.Auditing;

namespace ServerManager.Plugin.Git.GitHub;

public sealed class GitHubAuditActionProvider : IAuditActionProvider
{
    public IReadOnlyDictionary<string, string> GetDisplayNames() => new Dictionary<string, string>
    {
        [GitHubAuditActions.AppCreate] = "GitHub App eklendi",
        [GitHubAuditActions.AppDelete] = "GitHub App kaldırıldı"
    };
}
