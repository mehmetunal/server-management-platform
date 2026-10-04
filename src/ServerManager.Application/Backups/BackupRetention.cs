namespace ServerManager.Application.Backups;

public static class BackupRetention
{
    public const int MinKeepLast = 1;
    public const int MaxKeepLast = 365;
    public const int MaxKeepDays = 3650;

    /// <summary>
    /// Silinebilecek yedekler: en yeni <paramref name="keepLast"/> yedeğin dışında kalan ve (<paramref name="keepDays"/> &gt; 0 ise)
    /// o günden eski olanlar. En yeni yedek hiçbir koşulda silinmez.
    /// </summary>
    public static IReadOnlyList<T> SelectExpired<T>(
        IEnumerable<T> backups,
        Func<T, DateTime> completedAt,
        int keepLast,
        int keepDays,
        DateTime nowUtc)
    {
        var keep = Math.Clamp(keepLast, MinKeepLast, MaxKeepLast);
        var cutoff = keepDays > 0 ? nowUtc.AddDays(-Math.Min(keepDays, MaxKeepDays)) : (DateTime?)null;

        return backups
            .OrderByDescending(completedAt)
            .Skip(keep)
            .Where(b => cutoff is null || completedAt(b) < cutoff)
            .ToList();
    }
}
