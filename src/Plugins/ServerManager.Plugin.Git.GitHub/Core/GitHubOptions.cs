namespace ServerManager.Plugin.Git.GitHub.Core;

public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    /// <summary>GitHub REST API kök adresi. GitHub Enterprise Server için <c>https://host/api/v3</c>.</summary>
    public string ApiUrl { get; set; } = "https://api.github.com";

    /// <summary>GitHub web adresi; uygulama oluşturma ve kurulum sayfaları buradan açılır.</summary>
    public string WebUrl { get; set; } = "https://github.com";

    /// <summary>
    /// Panelin tarayıcıdan erişilen adresi (ör. https://panel.example.com). Boşsa isteğin adresi kullanılır;
    /// ters vekil arkasında doğru dönüş adresi için doldurulmalıdır.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    public int HttpTimeoutSeconds { get; set; } = 15;

    /// <summary>Kurulum ve depo listelerinin proje formu için önbellekte tutulma süresi.</summary>
    public int ListCacheSeconds { get; set; } = 60;
}
