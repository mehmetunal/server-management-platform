using System.Globalization;
using ServerManager.Application.Deployments;

namespace ServerManager.Application.Backups;

/// <summary>Çalışma logunu sınırlı boyutta tutar ve her satırda kalıcı kayda yazar (detay sayfası bunu yoklar).</summary>
public sealed class BackupRunLog
{
    private readonly DeploymentLog _log;
    private readonly Func<string, CancellationToken, Task> _persist;
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;
    private readonly SemaphoreSlim _persistGate = new(1, 1);

    public BackupRunLog(int maxChars, Func<string, CancellationToken, Task> persist, TimeProvider timeProvider, TimeZoneInfo timeZone)
    {
        _log = new DeploymentLog(maxChars);
        _persist = persist;
        _timeProvider = timeProvider;
        _timeZone = timeZone;
    }

    public Task InfoAsync(string message) => WriteAsync(message);

    public Task ErrorAsync(string message) => WriteAsync("HATA: " + message);

    private async Task WriteAsync(string message)
    {
        var local = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _timeZone);
        _log.Append($"[{local.ToString("HH:mm:ss", CultureInfo.InvariantCulture)}] {message}\n");
        await _persistGate.WaitAsync();
        try
        {
            await _persist(_log.ToString(), CancellationToken.None);
        }
        catch (Exception)
        {
            // Log kaydı yazılamasa da çalışma sürer; son durum tamamlanırken yeniden yazılır.
        }
        finally
        {
            _persistGate.Release();
        }
    }

    public override string ToString() => _log.ToString();

    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => (bytes / 1024d).ToString("0.0", CultureInfo.GetCultureInfo("tr-TR")) + " KB",
        < 1024L * 1024 * 1024 => (bytes / 1024d / 1024d).ToString("0.0", CultureInfo.GetCultureInfo("tr-TR")) + " MB",
        _ => (bytes / 1024d / 1024d / 1024d).ToString("0.00", CultureInfo.GetCultureInfo("tr-TR")) + " GB"
    };
}
