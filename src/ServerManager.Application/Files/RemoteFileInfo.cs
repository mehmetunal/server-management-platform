namespace ServerManager.Application.Files;

/// <param name="Mode">İzin bitleri (setuid/setgid/sticky dahil, dosya türü hariç).</param>
public sealed record RemoteFileInfo(
    string Name,
    string FullPath,
    RemoteFileKind Kind,
    long Size,
    DateTime LastWriteTimeUtc,
    int Mode,
    int UserId,
    int GroupId)
{
    /// <summary>Eşzamanlı düzenleme kontrolü için içerik sürümü (SFTP saniye çözünürlüğünde mtime ve boyut).</summary>
    public string Version => $"{new DateTimeOffset(DateTime.SpecifyKind(LastWriteTimeUtc, DateTimeKind.Utc)).ToUnixTimeSeconds()}-{Size}";
}
