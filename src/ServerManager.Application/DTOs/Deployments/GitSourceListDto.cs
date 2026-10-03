namespace ServerManager.Application.DTOs.Deployments;

public sealed class GitSourceListDto
{
    public IReadOnlyList<GitSourceOptionDto> Sources { get; init; } = [];

    /// <summary>Bağlantıları okunamayan entegrasyonların hata mesajları; diğer entegrasyonlar yine listelenir.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
