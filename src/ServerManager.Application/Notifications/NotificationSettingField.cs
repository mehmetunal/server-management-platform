namespace ServerManager.Application.Notifications;

/// <summary>
/// Eklenti ayar formunda (bildirim kanalı, yedek depolama) gösterilen alan. <see cref="NotificationFieldType.Secret"/> alanlar arayüzde geri
/// gösterilmez; düzenlemede boş bırakılırsa kayıtlı değer korunur.
/// </summary>
public sealed record NotificationSettingField(
    string Key,
    string Label,
    NotificationFieldType Type = NotificationFieldType.Text,
    bool IsRequired = true,
    string? Hint = null,
    string? Placeholder = null,
    string? DefaultValue = null,
    int MaxLength = 500,
    IReadOnlyList<NotificationFieldOption>? Options = null)
{
    public bool IsSecret => Type == NotificationFieldType.Secret;
}
