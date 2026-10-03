namespace ServerManager.Application.Auditing;

public static class AuditActions
{
    public const string Login = "auth.login";
    public const string LoginFailed = "auth.login_failed";
    public const string Logout = "auth.logout";

    public const string ServerCreate = "server.create";
    public const string ServerUpdate = "server.update";
    public const string ServerDelete = "server.delete";
    public const string ServerConnectionTest = "server.connection_test";

    public const string UserCreate = "user.create";
    public const string UserUpdate = "user.update";
    public const string UserLock = "user.lock";
    public const string UserUnlock = "user.unlock";

    public static readonly IReadOnlyDictionary<string, string> DisplayNames = new Dictionary<string, string>
    {
        [Login] = "Giriş",
        [LoginFailed] = "Başarısız giriş",
        [Logout] = "Çıkış",
        [ServerCreate] = "Sunucu ekleme",
        [ServerUpdate] = "Sunucu güncelleme",
        [ServerDelete] = "Sunucu silme",
        [ServerConnectionTest] = "Bağlantı testi",
        [UserCreate] = "Kullanıcı ekleme",
        [UserUpdate] = "Kullanıcı güncelleme",
        [UserLock] = "Kullanıcı kilitleme",
        [UserUnlock] = "Kullanıcı kilidi açma"
    };
}
