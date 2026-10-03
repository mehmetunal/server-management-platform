namespace ServerManager.Application.Terminal;

public enum DangerousCommandMode
{
    /// <summary>Kontrol yapılmaz.</summary>
    Off = 0,

    /// <summary>Komut çalışır; kullanıcıya uyarı gösterilir ve audit'e yazılır.</summary>
    Warn = 1,

    /// <summary>Komut, kullanıcı ikinci kez onaylayana kadar bekletilir.</summary>
    Confirm = 2,

    /// <summary>Komut sunucuya gönderilmez.</summary>
    Block = 3
}
