using ServerManager.Infrastructure.Deployments;

namespace ServerManager.Application.Tests.Deployments;

public class DeploymentOutputParserTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public void Parses_branch_names_from_ls_remote()
    {
        var output = $"{Sha}\trefs/heads/main\n{Sha}\trefs/heads/feature/x\n{Sha}\trefs/tags/v1\n{Sha}\trefs/heads/main\nwarning: noise\n";

        Assert.Equal(["feature/x", "main"], DeploymentOutputParser.ParseBranches(output));
    }

    [Fact]
    public void Parses_commit_from_last_line()
    {
        var output = $"HEAD is now at x\n{Sha.ToUpperInvariant()}\u001fAyşe Yılmaz\u001fFix: giriş sayfası\n";

        var commit = DeploymentOutputParser.ParseCommit(output);

        Assert.NotNull(commit);
        Assert.Equal(Sha, commit.Sha);
        Assert.Equal("Ayşe Yılmaz", commit.Author);
        Assert.Equal("Fix: giriş sayfası", commit.Subject);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-sha\u001fa\u001fb")]
    public void Invalid_commit_output_returns_null(string output) => Assert.Null(DeploymentOutputParser.ParseCommit(output));

    [Fact]
    public void Missing_author_and_subject_are_null()
    {
        var commit = DeploymentOutputParser.ParseCommit($"{Sha}\u001f\u001f");

        Assert.NotNull(commit);
        Assert.Null(commit.Author);
        Assert.Null(commit.Subject);
    }
}
