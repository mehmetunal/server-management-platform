namespace ServerManager.Application.DTOs.Settings;

public sealed record SettingSectionDto(string Title, IReadOnlyList<SettingItemDto> Items);
