using ServerManager.Application.Backups;

namespace ServerManager.Application.Tests.Backups;

public class BackupRetentionTests
{
    private static readonly DateTime Now = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    private static List<DateTime> DaysAgo(params int[] days) => days.Select(d => Now.AddDays(-d)).ToList();

    [Fact]
    public void Keeps_the_newest_n_backups()
    {
        var expired = BackupRetention.SelectExpired(DaysAgo(0, 1, 2, 3, 4), d => d, keepLast: 3, keepDays: 0, Now);

        Assert.Equal(DaysAgo(3, 4), expired);
    }

    [Fact]
    public void Keep_days_protects_recent_backups_beyond_the_count()
    {
        var expired = BackupRetention.SelectExpired(DaysAgo(0, 1, 2, 10, 20), d => d, keepLast: 1, keepDays: 7, Now);

        Assert.Equal(DaysAgo(10, 20), expired);
    }

    [Fact]
    public void Newest_backup_is_never_selected_even_with_zero_keep_last()
    {
        var expired = BackupRetention.SelectExpired(DaysAgo(100, 200), d => d, keepLast: 0, keepDays: 1, Now);

        Assert.Equal(DaysAgo(200), expired);
    }

    [Fact]
    public void Order_of_input_does_not_matter()
    {
        var expired = BackupRetention.SelectExpired(DaysAgo(4, 0, 3, 1, 2), d => d, keepLast: 2, keepDays: 0, Now);

        Assert.Equal(DaysAgo(2, 3, 4), expired);
    }

    [Fact]
    public void Nothing_expires_when_under_the_limit()
    {
        Assert.Empty(BackupRetention.SelectExpired(DaysAgo(0, 1), d => d, keepLast: 7, keepDays: 0, Now));
    }
}
