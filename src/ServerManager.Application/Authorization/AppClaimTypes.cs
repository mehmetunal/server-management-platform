namespace ServerManager.Application.Authorization;

public static class AppClaimTypes
{
    /// <summary>"1" ise hesapta iki adımlı doğrulama açıktır. Oturum yenilendiğinde (security stamp doğrulaması) güncellenir.</summary>
    public const string TwoFactorEnabled = "sm:2fa";
}
