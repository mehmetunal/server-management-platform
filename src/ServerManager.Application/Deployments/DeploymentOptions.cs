namespace ServerManager.Application.Deployments;

public sealed class DeploymentOptions
{
    public const string SectionName = "Deployment";

    /// <summary>Depo erişim kontrolü, fetch ve checkout adımlarının zaman aşımı.</summary>
    public int GitTimeoutSeconds { get; set; } = 300;

    public int BuildTimeoutMinutes { get; set; } = 30;

    public int DeployTimeoutMinutes { get; set; } = 10;

    /// <summary>Deployment kaydında saklanan log (son kısım).</summary>
    public int MaxStoredLogKilobytes { get; set; } = 1024;

    /// <summary>Let's Encrypt hesap e-postası. Panel bu adresle sertifika ister.</summary>
    public string AcmeEmail { get; set; } = string.Empty;

    /// <summary>
    /// Dockerfile projelerinde başarılı deploy sonrası saklanan commit imajı sayısı (<c>sm-&lt;slug&gt;:&lt;kısa-sha&gt;</c>);
    /// daha eskiler silinir. Geri dönüş bu imajlardan build etmeden yapılır. 0 veya negatif ise imaj silinmez.
    /// </summary>
    public int KeepImageCount { get; set; } = 5;

    /// <summary>
    /// Webhook adresinde kullanılacak panel adresi (ör. https://panel.ornek.com). Boşsa istek yapılan adres kullanılır;
    /// panel ters proxy arkasındaysa ve Git sağlayıcısı başka bir adresten erişecekse doldurun.
    /// </summary>
    public string? PublicBaseUrl { get; set; }
}
