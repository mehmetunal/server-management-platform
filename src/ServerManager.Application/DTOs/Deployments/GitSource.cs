namespace ServerManager.Application.DTOs.Deployments;

public sealed class GitSource
{
    public required string RepositoryUrl { get; init; }

    /// <summary>HTTPS erişim anahtarıyla birlikte gönderilen kullanıcı adı.</summary>
    public string? Username { get; init; }

    public string? AccessToken { get; init; }

    public override string ToString() => RepositoryUrl;
}
