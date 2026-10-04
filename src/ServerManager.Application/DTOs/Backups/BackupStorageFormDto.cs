namespace ServerManager.Application.DTOs.Backups;

public sealed class BackupStorageFormDto
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ProviderSystemName { get; set; } = string.Empty;

    /// <summary>Sağlayıcı alanları. Gizli alanlar düzenleme formunda boş gelir; boş bırakılırsa kayıtlı değer korunur.</summary>
    public Dictionary<string, string?> Settings { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Düzenleme formunda kayıtlı değeri olan gizli alanlar (değerleri gösterilmez).</summary>
    public List<string> StoredSecretKeys { get; set; } = [];
}
