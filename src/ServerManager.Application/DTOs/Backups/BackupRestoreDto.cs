namespace ServerManager.Application.DTOs.Backups;

public sealed class BackupRestoreDto
{
    public Guid RunId { get; set; }

    public Guid? TargetServerId { get; set; }

    /// <summary>Dosya yedeği: arşivin açılacağı klasör; "/" özgün konumlara yazar.</summary>
    public string? TargetDirectory { get; set; }

    public string? TargetVolume { get; set; }

    /// <summary>Veritabanı yedeği: boşsa işteki container (veya sunucudaki istemci) kullanılır.</summary>
    public string? TargetContainer { get; set; }

    /// <summary>MongoDB'de tüm veritabanlarının yedeği için boş bırakılır (özgün adlarla yüklenir).</summary>
    public string? TargetDatabase { get; set; }

    /// <summary>MongoDB: yüklemeden önce yedekteki koleksiyonları sil (<c>mongorestore --drop</c>).</summary>
    public bool DropExisting { get; set; }

    /// <summary>Kullanıcı hedefteki verinin üzerine yazılacağını onayladı.</summary>
    public bool Confirmed { get; set; }
}
