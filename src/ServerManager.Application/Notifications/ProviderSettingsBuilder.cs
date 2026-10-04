using ServerManager.Application.Common;

namespace ServerManager.Application.Notifications;

/// <summary>Eklenti ayar formunu (bildirim kanalı, yedek depolama) sağlayıcının alan tanımına göre doğrular.</summary>
public static class ProviderSettingsBuilder
{
    public const string SettingsPrefix = "Settings";

    /// <summary>Yalnızca sağlayıcının tanımladığı alanlar saklanır; boş gizli alan kayıtlı değeri korur.</summary>
    public static (Dictionary<string, string> Settings, List<ServiceError> Errors) Build(
        IReadOnlyList<NotificationSettingField> fields,
        Func<IReadOnlyDictionary<string, string>, IReadOnlyList<ServiceError>> validate,
        IReadOnlyDictionary<string, string?> input,
        IReadOnlyDictionary<string, string>? stored)
    {
        var settings = new Dictionary<string, string>(StringComparer.Ordinal);
        var errors = new List<ServiceError>();

        foreach (var field in fields)
        {
            var key = $"{SettingsPrefix}[{field.Key}]";
            var value = input.GetValueOrDefault(field.Key)?.Trim();
            if (string.IsNullOrEmpty(value) && field.IsSecret && stored is not null && stored.TryGetValue(field.Key, out var existing))
                value = existing;
            if (string.IsNullOrEmpty(value))
                value = field.IsSecret ? null : field.DefaultValue;

            if (string.IsNullOrEmpty(value))
            {
                if (field.IsRequired)
                    errors.Add(new ServiceError(key, $"{field.Label} zorunludur."));
                continue;
            }

            if (value.Length > field.MaxLength)
            {
                errors.Add(new ServiceError(key, $"{field.Label} en fazla {field.MaxLength} karakter olabilir."));
                continue;
            }

            var formatError = field.Type switch
            {
                NotificationFieldType.Number when !int.TryParse(value, out _) => $"{field.Label} bir sayı olmalıdır.",
                NotificationFieldType.Select when field.Options is { Count: > 0 } options && options.All(o => o.Value != value) => $"{field.Label} için geçerli bir seçenek seçin.",
                NotificationFieldType.Url when !Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") => $"{field.Label} http(s):// ile başlayan geçerli bir adres olmalıdır.",
                _ => null
            };
            if (formatError is not null)
            {
                errors.Add(new ServiceError(key, formatError));
                continue;
            }

            settings[field.Key] = value;
        }

        if (errors.Count == 0)
        {
            errors.AddRange(validate(settings).Select(e =>
                new ServiceError(string.IsNullOrEmpty(e.PropertyName) ? string.Empty : $"{SettingsPrefix}[{e.PropertyName}]", e.Message)));
        }

        return (settings, errors);
    }

    public static bool AreEqual(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right) =>
        left.Count == right.Count && left.All(pair => right.TryGetValue(pair.Key, out var value) && value == pair.Value);
}
