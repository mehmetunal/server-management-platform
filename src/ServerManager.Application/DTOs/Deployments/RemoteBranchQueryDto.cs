using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Deployments;

/// <summary>Proje formunda, kaydetmeden önce depo adresindeki dalları hedef sunucudan okumak için.</summary>
public sealed class RemoteBranchQueryDto
{
    public Guid ServerId { get; set; }

    /// <summary>Düzenlenen proje; erişim anahtarı boşsa kayıtlı anahtar kullanılır.</summary>
    public Guid? ProjectId { get; set; }

    public GitProvider GitProvider { get; set; } = GitProvider.GitHub;

    public string RepositoryUrl { get; set; } = string.Empty;

    public string? GitUsername { get; set; }

    public string? AccessToken { get; set; }

    public bool RemoveAccessToken { get; set; }
}
