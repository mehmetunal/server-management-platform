using ServerManager.Application.Backups;

namespace ServerManager.Web.Backups;

public sealed class BackupActiveRun
{
    public BackupActiveRun(Guid id, Guid? jobId, BackupCancellation cancellation)
    {
        Id = id;
        JobId = jobId;
        Cancellation = cancellation;
    }

    public Guid Id { get; }

    /// <summary>Yedeklemede iş; geri yüklemede null (aynı yedekten birden fazla geri yükleme yapılabilir).</summary>
    public Guid? JobId { get; }

    public BackupCancellation Cancellation { get; }

    public Task Execution { get; set; } = Task.CompletedTask;

    public bool IsCompleted => Execution.IsCompleted;
}
