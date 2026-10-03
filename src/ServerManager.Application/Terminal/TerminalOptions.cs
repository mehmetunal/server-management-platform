namespace ServerManager.Application.Terminal;

public sealed class TerminalOptions
{
    public const string SectionName = "Terminal";

    public int IdleTimeoutMinutes { get; set; } = 30;

    public int MaxSessionsPerUser { get; set; } = 5;

    /// <summary>Tarayıcı bağlantısı koptuğunda SSH oturumunun yeniden bağlanma için açık tutulduğu süre.</summary>
    public int ReconnectGraceSeconds { get; set; } = 120;

    /// <summary>Yeniden bağlanınca ekrana tekrar basılan son çıktının üst sınırı.</summary>
    public int OutputBufferKilobytes { get; set; } = 256;

    /// <summary>Onay bekleyen tehlikeli komut bu süre içinde yanıtlanmazsa iptal edilir.</summary>
    public int ConfirmationTimeoutSeconds { get; set; } = 120;

    public DangerousCommandMode DangerousCommandMode { get; set; } = DangerousCommandMode.Confirm;

    /// <summary>Boş bırakılırsa <see cref="DangerousCommandRule.Defaults"/> kullanılır.</summary>
    public List<DangerousCommandRule> DangerousCommands { get; set; } = [];
}
