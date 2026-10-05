using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Deployments;

/// <summary>Ortam değişkenleri sekmesi: yalnızca anahtarlar; değerler ayrıca ve yetkiyle istenir.</summary>
public sealed class ProjectEnvironmentDto
{
    public Guid ProjectId { get; init; }

    public DeploymentBuildType BuildType { get; init; }

    /// <summary>.env dosyasındaki sırayla anahtarlar.</summary>
    public IReadOnlyList<string> Keys { get; init; } = [];

    /// <summary>Kayıtlı içerik var (boş da olsa); deploy'da .env yazılır.</summary>
    public bool HasEnvironment { get; init; }

    /// <summary>Kayıt master key ile çözülemedi; yalnızca "tümünü değiştir" ile içe aktarma yapılabilir.</summary>
    public bool Unreadable { get; init; }
}

/// <summary>Tek değişken ekleme / değer değiştirme formu.</summary>
public sealed class EnvironmentVariableDto
{
    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }

    /// <summary>true: yeni anahtar (varsa hata); false: var olan anahtarın değeri değişir (yoksa hata).</summary>
    public bool IsNew { get; set; }
}

public enum EnvironmentImportMode
{
    /// <summary>Yalnızca olmayan anahtarlar eklenir; var olanlar korunur.</summary>
    AddMissing = 0,

    /// <summary>Gelen anahtarlar eklenir veya değeri değişir; listede olmayanlar korunur.</summary>
    Overwrite = 1,

    /// <summary>Kayıtlı içerik tamamen gelen içerikle değiştirilir (çözülemeyen kayıt da bu yolla düzeltilir).</summary>
    Replace = 2
}

/// <summary>Toplu değişikliğin özeti; değerler yer almaz.</summary>
public sealed class EnvironmentChangeResultDto
{
    public IReadOnlyList<string> Added { get; init; } = [];

    public IReadOnlyList<string> Updated { get; init; } = [];

    /// <summary>Üzerine yazma kapalıyken var olduğu için değiştirilmeyen anahtarlar.</summary>
    public IReadOnlyList<string> Skipped { get; init; } = [];

    /// <summary>Tümünü değiştirme modunda kaldırılan anahtarlar.</summary>
    public IReadOnlyList<string> Removed { get; init; } = [];

    public bool HasChanges => Added.Count > 0 || Updated.Count > 0 || Removed.Count > 0;
}
