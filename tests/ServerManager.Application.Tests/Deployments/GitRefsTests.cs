using ServerManager.Application.Deployments;

namespace ServerManager.Application.Tests.Deployments;

public class GitRefsTests
{
    [Theory]
    [InlineData("main")]
    [InlineData("release/1.2")]
    [InlineData("feature/login-page_v2")]
    public void Accepts_regular_branch_names(string branch) => Assert.True(GitRefs.IsValidBranch(branch));

    [Theory]
    [InlineData("")]
    [InlineData("-upload-pack=evil")]
    [InlineData("/main")]
    [InlineData("main/")]
    [InlineData(".hidden")]
    [InlineData("main.")]
    [InlineData("main.lock")]
    [InlineData("a..b")]
    [InlineData("a//b")]
    [InlineData("a/.b")]
    [InlineData("main branch")]
    [InlineData("main;rm")]
    [InlineData("dal-ğ")]
    public void Rejects_unsafe_branch_names(string branch) => Assert.False(GitRefs.IsValidBranch(branch));

    [Fact]
    public void Rejects_overlong_branch() => Assert.False(GitRefs.IsValidBranch(new string('a', GitRefs.MaxBranchLength + 1)));

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef01234567", true)]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", true)]
    [InlineData("0123456", false)]
    [InlineData("0123456789ABCDEF0123456789abcdef01234567", false)]
    [InlineData("--all", false)]
    public void Commit_must_be_full_lowercase_hash(string commit, bool expected) => Assert.Equal(expected, GitRefs.IsValidCommit(commit));

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData(" ABCDEF ", "abcdef")]
    public void Normalizes_commit(string? input, string? expected) => Assert.Equal(expected, GitRefs.NormalizeCommit(input));

    [Fact]
    public void Short_sha_keeps_twelve_characters()
    {
        Assert.Equal("0123456789ab", GitRefs.ShortSha("0123456789abcdef0123456789abcdef01234567"));
        Assert.Equal("abc", GitRefs.ShortSha("abc"));
        Assert.Equal(string.Empty, GitRefs.ShortSha(null));
    }
}
