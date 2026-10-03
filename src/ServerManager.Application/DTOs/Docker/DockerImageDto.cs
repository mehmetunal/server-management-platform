namespace ServerManager.Application.DTOs.Docker;

public sealed class DockerImageDto
{
    public string Id { get; init; } = string.Empty;

    public string ShortId
    {
        get
        {
            var value = Id.StartsWith("sha256:", StringComparison.Ordinal) ? Id[7..] : Id;
            return value.Length > 12 ? value[..12] : value;
        }
    }

    public string Repository { get; init; } = string.Empty;

    public string Tag { get; init; } = string.Empty;

    public string? Digest { get; init; }

    public long SizeBytes { get; init; }

    public DateTime? CreatedAt { get; init; }

    public int? ContainerCount { get; init; }

    public bool IsDangling => Repository == "<none>";

    /// <summary>İşlemlerde kullanılacak referans: etiketliyse repository:tag, değilse image ID.</summary>
    public string Reference => IsDangling || Tag == "<none>" ? Id : $"{Repository}:{Tag}";
}
