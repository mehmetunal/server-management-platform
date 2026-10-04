using ServerManager.Application.DTOs.Settings;

namespace ServerManager.Web.Models;

public class SettingsPageViewModel
{
    public required SystemInfoDto Info { get; init; }

    public required IReadOnlyList<PanelSettingGroupDto> Groups { get; init; }
}
