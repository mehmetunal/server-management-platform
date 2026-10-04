namespace ServerManager.Web.Options;

public sealed class TwoFactorOptions
{
    public const string SectionName = "TwoFactor";

    /// <summary>
    /// Açıksa iki adımlı doğrulaması kapalı kullanıcılar giriş yaptıktan sonra yalnızca Hesabım sayfasını kullanabilir;
    /// kurulumu tamamlayınca panel açılır.
    /// </summary>
    public bool Required { get; set; }
}
