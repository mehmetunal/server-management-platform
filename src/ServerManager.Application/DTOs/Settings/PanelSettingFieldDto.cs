namespace ServerManager.Application.DTOs.Settings;

public sealed record PanelSettingFieldDto(
    string Key,
    string Label,
    string Kind,
    string Value,
    string Hint,
    double? Minimum,
    double? Maximum,
    string? Cluster);
