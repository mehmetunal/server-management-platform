namespace ServerManager.Plugin.Git.GitHub.DTOs;

/// <summary>GitHub'da önceden oluşturulmuş bir App'i uygulama kimliği ve özel anahtarıyla ekleme.</summary>
public sealed class GitHubManualAppDto
{
    public long AppId { get; set; }

    public string PrivateKey { get; set; } = string.Empty;
}
