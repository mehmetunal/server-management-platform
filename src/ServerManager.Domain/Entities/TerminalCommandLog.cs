using ServerManager.Domain.Enums;

namespace ServerManager.Domain.Entities;

public class TerminalCommandLog
{
    public long Id { get; set; }

    public Guid SessionId { get; set; }

    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

    public string CommandText { get; set; } = string.Empty;

    /// <summary>Komut satırı ok tuşları, Tab tamamlama veya geçmişten çağırma ile düzenlendiyse tam metin bilinmez.</summary>
    public bool IsApproximate { get; set; }

    public TerminalCommandStatus Status { get; set; } = TerminalCommandStatus.Executed;

    public string? MatchedRule { get; set; }
}
