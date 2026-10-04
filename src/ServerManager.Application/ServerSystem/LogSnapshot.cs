namespace ServerManager.Application.ServerSystem;

public sealed record LogSnapshot(
    LogSource Source,
    string SourceLabel,
    IReadOnlyList<string> Lines,
    bool HasJournal,
    IReadOnlyList<string> Files,
    string? Notice);
