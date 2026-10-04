namespace ServerManager.Application.Backups;

/// <summary>Şifreli yedek dosyası okunamadığında (yanlış parola, bozuk veya eksik dosya) fırlatılır; mesaj kullanıcıya gösterilebilir.</summary>
public sealed class BackupFormatException : Exception
{
    public BackupFormatException(string message)
        : base(message)
    {
    }

    public BackupFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
