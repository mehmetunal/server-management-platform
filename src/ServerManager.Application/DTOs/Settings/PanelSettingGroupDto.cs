namespace ServerManager.Application.DTOs.Settings;

public sealed record PanelSettingGroupDto(string Title, IReadOnlyList<PanelSettingFieldDto> Fields);
