namespace ServerManager.Application.DTOs.Settings;

public sealed record SystemInfoDto(
    string Version,
    string EnvironmentName,
    string Runtime,
    string OperatingSystem,
    DateTime StartedAtUtc,
    DatabaseStatusDto Database,
    IReadOnlyList<SettingSectionDto> Sections);
