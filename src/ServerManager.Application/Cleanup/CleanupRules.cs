using System.Text.RegularExpressions;

namespace ServerManager.Application.Cleanup;

public static partial class CleanupRules
{
    public const int MinDays = 1;
    public const int MaxDays = 3650;
    public const int MinJournalMegabytes = 16;
    public const int MaxJournalMegabytes = 1_000_000;

    /// <summary>Tek çalıştırmada işlenebilecek en fazla öğe.</summary>
    public const int MaxSelectedItems = 500;

    public static CleanupOptions Normalize(CleanupOptions? options)
    {
        options ??= new CleanupOptions();
        return new CleanupOptions
        {
            LogDays = options.LogDays <= 0 ? CleanupOptions.DefaultLogDays : Math.Clamp(options.LogDays, MinDays, MaxDays),
            TempDays = options.TempDays <= 0 ? CleanupOptions.DefaultTempDays : Math.Clamp(options.TempDays, MinDays, MaxDays),
            JournalMaxMegabytes = options.JournalMaxMegabytes <= 0
                ? CleanupOptions.DefaultJournalMaxMegabytes
                : Math.Clamp(options.JournalMaxMegabytes, MinJournalMegabytes, MaxJournalMegabytes)
        };
    }

    /// <summary>Seçim anahtarı biçimi; tarama sonucunda olmayan anahtarlar zaten yok sayılır, bu yalnızca çöp girdiyi erkenden eler.</summary>
    public static bool IsValidKey(string? key) =>
        !string.IsNullOrEmpty(key) && key.Length <= 400 && KeyPattern().IsMatch(key);

    [GeneratedRegex(@"^[a-z]+(:[A-Za-z0-9_.:/@|+-]+)?$")]
    private static partial Regex KeyPattern();
}
