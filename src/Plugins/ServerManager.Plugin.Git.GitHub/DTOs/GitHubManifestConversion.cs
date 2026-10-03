namespace ServerManager.Plugin.Git.GitHub.DTOs;

/// <summary>Manifest kodunun dönüştürülmesiyle GitHub'ın bir kez verdiği uygulama bilgileri ve gizli anahtarlar.</summary>
public sealed record GitHubManifestConversion(
    long Id,
    string Slug,
    string Name,
    string? ClientId,
    string? ClientSecret,
    string? WebhookSecret,
    string Pem,
    string? OwnerLogin,
    string? HtmlUrl);
