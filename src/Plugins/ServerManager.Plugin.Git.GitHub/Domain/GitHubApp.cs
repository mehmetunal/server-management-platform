using ServerManager.Domain.Common;

namespace ServerManager.Plugin.Git.GitHub.Domain;

/// <summary>Panele tanıtılmış GitHub App. Gizli alanlar <c>ISecretProtector</c> ile şifreli saklanır.</summary>
public class GitHubApp : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>GitHub'ın verdiği sayısal uygulama kimliği (JWT iss alanı).</summary>
    public long AppId { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string? OwnerLogin { get; set; }

    public string? HtmlUrl { get; set; }

    public string? ClientId { get; set; }

    public string? EncryptedClientSecret { get; set; }

    public string EncryptedPrivateKey { get; set; } = string.Empty;

    public string? EncryptedWebhookSecret { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }
}
