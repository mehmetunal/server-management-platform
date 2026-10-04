namespace ServerManager.Application.ServerSystem;

public sealed class LogRequest
{
    public const int DefaultLines = 200;
    public const int MaxLines = 2000;

    public LogSource Source { get; set; } = LogSource.Journal;

    /// <summary>Journal kaynağında isteğe bağlı systemd unit adı.</summary>
    public string? Unit { get; set; }

    /// <summary>Dosya kaynağında /var/log altındaki yol.</summary>
    public string? Path { get; set; }

    /// <summary>Journal öncelik eşiği (0 emerg … 7 debug).</summary>
    public int? Priority { get; set; }

    public int Lines { get; set; } = DefaultLines;

    /// <summary>Satırlar panelde süzülür (büyük/küçük harf duyarsız); sunucuda komut olarak çalıştırılmaz.</summary>
    public string? Filter { get; set; }
}
