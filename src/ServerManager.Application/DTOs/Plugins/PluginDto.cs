namespace ServerManager.Application.DTOs.Plugins;

public sealed record PluginDto
{
    public required string SystemName { get; init; }

    public required string FriendlyName { get; init; }

    public required string Group { get; init; }

    public required string Version { get; init; }

    public string? Author { get; init; }

    public string? Description { get; init; }

    public bool IsLoaded { get; init; }

    public string? LoadError { get; init; }

    public bool IsInstalled { get; init; }

    public bool IsEnabled { get; init; }

    public string? InstalledVersion { get; init; }

    public DateTime? InstalledAt { get; init; }

    public string? InstalledBy { get; init; }
}
