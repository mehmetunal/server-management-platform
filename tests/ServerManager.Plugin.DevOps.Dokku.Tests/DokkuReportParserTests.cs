using ServerManager.Plugin.DevOps.Dokku.Integration;

namespace ServerManager.Plugin.DevOps.Dokku.Tests;

public class DokkuReportParserTests
{
    [Fact]
    public void Missing_dokku_is_not_installed()
    {
        var report = DokkuReportParser.Parse("SM_STATE=missing\n");

        Assert.NotNull(report);
        Assert.False(report.IsInstalled);
        Assert.Empty(report.Apps);
    }

    [Fact]
    public void Installed_report_keeps_apps_and_domains()
    {
        var report = DokkuReportParser.Parse(
            """
            SM_STATE=installed
            SM_VERSION=dokku version 0.38.31
            SM_APP=demo|true|true|demo.example.com other.example.com
            SM_APP=api|false|false|
            SM_APP=../evil|true|true|x
            """);

        Assert.NotNull(report);
        Assert.True(report.IsInstalled);
        Assert.Equal("dokku version 0.38.31", report.Version);
        Assert.Equal(2, report.Apps.Count);
        Assert.Equal("demo", report.Apps[0].Name);
        Assert.True(report.Apps[0].Deployed);
        Assert.True(report.Apps[0].Running);
        Assert.Equal("demo.example.com other.example.com", report.Apps[0].Domains);
        Assert.Equal("api", report.Apps[1].Name);
        Assert.False(report.Apps[1].Deployed);
        Assert.Null(report.Apps[1].Domains);
    }

    [Fact]
    public void Output_without_state_is_rejected()
    {
        Assert.Null(DokkuReportParser.Parse("dokku version 0.38.31"));
    }

    [Fact]
    public void Version_and_app_name_reject_shell_metacharacters()
    {
        Assert.True(DokkuCommands.IsVersion("v0.38.31"));
        Assert.False(DokkuCommands.IsVersion("v0.38.31;id"));
        Assert.False(DokkuCommands.IsVersion("latest"));
        Assert.True(DokkuCommands.IsAppName("my-app"));
        Assert.False(DokkuCommands.IsAppName("MyApp"));
        Assert.False(DokkuCommands.IsAppName("-app"));
        Assert.False(DokkuCommands.IsAppName("app name"));
    }

    [Fact]
    public void Install_command_uses_only_the_validated_version()
    {
        var command = DokkuCommands.Install("v0.38.31");

        Assert.Contains("https://dokku.com/install/v0.38.31/bootstrap.sh", command, StringComparison.Ordinal);
        Assert.Contains("DOKKU_TAG=", command, StringComparison.Ordinal);
        Assert.DoesNotContain(";", command.Split("bash -c ", 2)[0], StringComparison.Ordinal);
    }
}
