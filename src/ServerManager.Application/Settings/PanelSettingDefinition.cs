namespace ServerManager.Application.Settings;

public sealed record PanelSettingDefinition(
    string Key,
    string Section,
    string Label,
    PanelSettingKind Kind,
    double Minimum,
    double Maximum,
    string Hint);
