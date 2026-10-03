using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServerManager.Application.Terminal;

namespace ServerManager.Application.Tests.Terminal;

public class DangerousCommandDetectorTests
{
    private static DangerousCommandDetector Create(DangerousCommandMode mode = DangerousCommandMode.Confirm, params DangerousCommandRule[] rules) =>
        new(Options.Create(new TerminalOptions { DangerousCommandMode = mode, DangerousCommands = [.. rules] }),
            NullLogger<DangerousCommandDetector>.Instance);

    [Theory]
    [InlineData("rm -rf /var/www")]
    [InlineData("sudo rm -r build")]
    [InlineData("rm --recursive logs")]
    [InlineData("rm -f -R tmp")]
    [InlineData("cd /tmp && rm -fr x")]
    [InlineData("mkfs.ext4 /dev/sdb1")]
    [InlineData("dd if=/dev/zero of=/dev/sda bs=1M")]
    [InlineData("shutdown -h now")]
    [InlineData("sudo reboot")]
    [InlineData("systemctl reboot")]
    [InlineData("/sbin/poweroff")]
    [InlineData("iptables -F")]
    [InlineData("ufw disable")]
    [InlineData("userdel -r deploy")]
    [InlineData("cat image.iso > /dev/sda")]
    [InlineData(":(){ :|:& };:")]
    [InlineData("echo ok\nRM -RF /")]
    public void Detects_default_dangerous_commands(string command)
    {
        var match = Create().Detect(command);

        Assert.NotNull(match);
        Assert.Equal(DangerousCommandMode.Confirm, match.Mode);
    }

    [Theory]
    [InlineData("ls -la")]
    [InlineData("rm file.txt")]
    [InlineData("rm -f file.txt")]
    [InlineData("git rm --cached x")]
    [InlineData("echo firmware")]
    [InlineData("echo rebooted")]
    [InlineData("docker add")]
    [InlineData("")]
    public void Ignores_safe_commands(string command)
    {
        Assert.Null(Create().Detect(command));
    }

    [Fact]
    public void Off_mode_never_matches()
    {
        Assert.Null(Create(DangerousCommandMode.Off).Detect("rm -rf /"));
    }

    [Fact]
    public void Custom_rules_replace_defaults_and_invalid_patterns_are_skipped()
    {
        var detector = Create(
            DangerousCommandMode.Block,
            new DangerousCommandRule { Pattern = "(", Description = "bozuk" },
            new DangerousCommandRule { Pattern = @"\bdrop\s+database\b", Description = "Veritabanı silme" });

        var match = detector.Detect("mysql -e 'DROP DATABASE app'");

        Assert.Equal(new DangerousCommandMatch("Veritabanı silme", DangerousCommandMode.Block), match);
        Assert.Null(detector.Detect("rm -rf /"));
    }
}
