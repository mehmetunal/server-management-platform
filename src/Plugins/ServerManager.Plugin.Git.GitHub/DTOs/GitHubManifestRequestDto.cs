namespace ServerManager.Plugin.Git.GitHub.DTOs;

public sealed class GitHubManifestRequestDto
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Boşsa uygulama giriş yapmış kullanıcının hesabında oluşturulur.</summary>
    public string? Organization { get; set; }
}
