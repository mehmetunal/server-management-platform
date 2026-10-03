namespace ServerManager.Application.DTOs.Docker;

public sealed class DockerLogLineDto
{
    public DateTime? Timestamp { get; init; }

    /// <summary>Docker'ın verdiği nanosaniye hassasiyetli zaman damgası; canlı takipte --since için kullanılır.</summary>
    public string? RawTimestamp { get; init; }

    public string Text { get; init; } = string.Empty;

    public bool IsError { get; init; }
}
