namespace ServerManager.Application.ApiKeys;

/// <summary>Kişisel API anahtarları ve REST API (/api/v1) ayarları. Bölüm: <c>ApiKeys</c>.</summary>
public sealed class ApiKeyOptions
{
    public const string SectionName = "ApiKeys";

    /// <summary>false ise yeni anahtar oluşturulamaz ve /api/v1 tüm isteklere 401 döner.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Bir anahtarın en uzun geçerlilik süresi (gün). Seçenekler bu değerle sınırlanır.</summary>
    public int MaxLifetimeDays { get; set; } = 365;

    /// <summary>true ise süresiz anahtar oluşturulabilir. Varsayılan kapalı.</summary>
    public bool AllowNoExpiry { get; set; }

    /// <summary>Anahtar başına dakikadaki en çok API isteği.</summary>
    public int RequestsPerMinute { get; set; } = 120;

    /// <summary>Kullanıcı başına en çok etkin (iptal edilmemiş, süresi dolmamış) anahtar.</summary>
    public int MaxKeysPerUser { get; set; } = 20;
}
