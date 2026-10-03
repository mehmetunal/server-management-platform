using ServerManager.Application.DTOs.Ssh;
using ServerManager.Infrastructure.Deployments;

namespace ServerManager.Application.Tests.Deployments;

public class DeploymentErrorTranslatorTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    private static RemoteCommandOutput Failed(string stderr, int exitCode = 128) => new() { ExitCode = exitCode, Stderr = stderr };

    [Theory]
    [InlineData("fatal: could not read Username for 'https://github.com': terminal prompts disabled", "erişim anahtarı")]
    [InlineData("remote: Repository not found.\nfatal: repository 'https://github.com/a/b.git/' not found", "Depo bulunamadı")]
    [InlineData("fatal: couldn't find remote ref refs/heads/develop", "Dal depoda bulunamadı")]
    [InlineData("error: Server does not allow request for unadvertised object abc", "Commit depoda bulunamadı")]
    [InlineData("git@github.com: Permission denied (publickey).", "deploy key")]
    [InlineData("fatal: detected dubious ownership in repository at '/srv/app'", "sahibini")]
    [InlineData("ssh: Could not resolve hostname git.local: Name or service not known", "çözümlenemedi")]
    public void Translates_known_git_errors(string stderr, string fragment) =>
        Assert.Contains(fragment, DeploymentErrorTranslator.TranslateGit(Failed(stderr), Timeout));

    [Fact]
    public void Unknown_git_error_falls_back_to_last_fatal_line() =>
        Assert.Equal("Git: fatal: something odd", DeploymentErrorTranslator.TranslateGit(Failed("hint: x\nfatal: something odd\nhint: y"), Timeout));

    [Fact]
    public void Git_timeout_and_missing_binary_are_reported()
    {
        Assert.Equal("Git işlemi 5 dakika içinde bitmedi.", DeploymentErrorTranslator.TranslateGit(new RemoteCommandOutput { TimedOut = true }, Timeout));
        Assert.Contains("git kurulu değil", DeploymentErrorTranslator.TranslateGit(Failed("sh: git: not found", 127), Timeout));
    }

    [Fact]
    public void Docker_build_failure_shows_solver_message()
    {
        var stderr = "#5 [2/3] RUN npm ci\n#5 ERROR: process did not complete\nERROR: failed to solve: process \"/bin/sh -c npm ci\" did not complete successfully: exit code: 1";

        Assert.Equal(
            "Build başarısız: process \"/bin/sh -c npm ci\" did not complete successfully: exit code: 1",
            DeploymentErrorTranslator.TranslateDocker(Failed(stderr, 1), "Build", Timeout));
    }

    [Fact]
    public void Docker_daemon_error_is_extracted() =>
        Assert.Equal(
            "Deploy başarısız: driver failed programming external connectivity: port is already allocated",
            DeploymentErrorTranslator.TranslateDocker(
                Failed("Error response from daemon: driver failed programming external connectivity: port is already allocated", 125), "Deploy", Timeout));

    [Fact]
    public void Docker_access_errors_use_docker_translator() =>
        Assert.Contains("Docker", DeploymentErrorTranslator.TranslateDocker(
            Failed("permission denied while trying to connect to the docker API at unix:///var/run/docker.sock", 1), "Build", Timeout));

    [Fact]
    public void Command_failure_reports_exit_code_and_timeout()
    {
        Assert.Equal("Deploy komutu hata ile bitti (çıkış kodu 2); ayrıntılar konsolda.", DeploymentErrorTranslator.TranslateCommand(Failed("boom", 2), "Deploy", Timeout));
        Assert.Equal("Build komutu 5 dakika içinde bitmedi.", DeploymentErrorTranslator.TranslateCommand(new RemoteCommandOutput { TimedOut = true }, "Build", Timeout));
        Assert.Contains("program bulunamadı: sh: npm: not found", DeploymentErrorTranslator.TranslateCommand(Failed("sh: npm: not found", 127), "Build", Timeout));
    }
}
