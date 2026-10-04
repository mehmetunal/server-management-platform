using System.Text;
using ServerManager.Plugin.DevOps.Dokku.DTOs;

namespace ServerManager.Plugin.DevOps.Dokku.Installation;

public sealed class DokkuInstallRun
{
    private const int MaxLogChars = 180_000;

    private readonly object _gate = new();
    private readonly StringBuilder _log = new();

    public bool IsRunning { get; private set; } = true;

    public bool? Succeeded { get; private set; }

    public string? Message { get; private set; }

    public void Append(string line)
    {
        if (string.IsNullOrEmpty(line))
            return;

        lock (_gate)
        {
            _log.AppendLine(line.TrimEnd('\r'));
            if (_log.Length > MaxLogChars)
                _log.Remove(0, _log.Length - MaxLogChars);
        }
    }

    public void Complete(bool succeeded, string message)
    {
        lock (_gate)
        {
            IsRunning = false;
            Succeeded = succeeded;
            Message = message;
        }
    }

    public DokkuInstallProgress Snapshot()
    {
        lock (_gate)
        {
            return new DokkuInstallProgress
            {
                IsRunning = IsRunning,
                Succeeded = Succeeded,
                Message = Message,
                Log = _log.ToString()
            };
        }
    }
}
