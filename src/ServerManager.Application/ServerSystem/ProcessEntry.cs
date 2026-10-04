namespace ServerManager.Application.ServerSystem;

public sealed record ProcessEntry(
    int Pid,
    int? ParentPid,
    string User,
    double? CpuPercent,
    double? MemoryPercent,
    long? ResidentKilobytes,
    long? ElapsedSeconds,
    string Command)
{
    /// <summary>Komutun ilk parçasının dosya adı; çekirdek iş parçacıkları köşeli parantezle gelir.</summary>
    public string Name
    {
        get
        {
            var first = Command.Split(' ', 2)[0];
            if (first.StartsWith('['))
                return Command;
            var slash = first.LastIndexOf('/');
            return slash >= 0 && slash < first.Length - 1 ? first[(slash + 1)..] : first;
        }
    }
}
