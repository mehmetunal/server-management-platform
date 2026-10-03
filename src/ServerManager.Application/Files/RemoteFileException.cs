namespace ServerManager.Application.Files;

/// <summary>Uzak dosya işleminin protokolden bağımsız hatası.</summary>
public sealed class RemoteFileException : Exception
{
    public RemoteFileException(RemoteFileErrorKind kind, string? detail = null, Exception? innerException = null)
        : base(detail ?? kind.ToString(), innerException)
    {
        Kind = kind;
        Detail = detail;
    }

    public RemoteFileErrorKind Kind { get; }

    public string? Detail { get; }
}
