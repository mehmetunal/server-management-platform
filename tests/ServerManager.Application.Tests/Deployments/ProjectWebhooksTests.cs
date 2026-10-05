using System.Security.Cryptography;
using System.Text;
using ServerManager.Application.Deployments;

namespace ServerManager.Application.Tests.Deployments;

public class ProjectWebhooksTests
{
    private const string Secret = "s3cr3t-anahtar";
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    private static string Sign(string body, string secret = Secret) =>
        "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));

    private static WebhookDelivery GitHub(string eventName, string body, string? signature = null) =>
        new(Encoding.UTF8.GetBytes(body), GitHubEvent: eventName, GitHubSignature256: signature ?? Sign(body));

    private static WebhookDelivery GitLab(string eventName, string body, string token = Secret) =>
        new(Encoding.UTF8.GetBytes(body), GitLabEvent: eventName, GitLabToken: token);

    private static string Push(string reference, string after = Sha, bool deleted = false) =>
        $"{{\"ref\":\"{reference}\",\"after\":\"{after}\",\"deleted\":{(deleted ? "true" : "false")},\"pusher\":{{\"name\":\"ayse\"}}}}";

    [Fact]
    public void GitHub_signature_is_verified_over_the_raw_body()
    {
        var body = Push("refs/heads/main");

        Assert.True(ProjectWebhooks.VerifyGitHubSignature(Secret, Encoding.UTF8.GetBytes(body), Sign(body)));
        Assert.True(ProjectWebhooks.VerifyGitHubSignature(Secret, Encoding.UTF8.GetBytes(body), Sign(body).ToUpperInvariant().Replace("SHA256=", "sha256=", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sha1=abcdef")]
    [InlineData("sha256=not-hex")]
    [InlineData("sha256=00")]
    public void Malformed_github_signatures_are_rejected(string? header) =>
        Assert.False(ProjectWebhooks.VerifyGitHubSignature(Secret, Encoding.UTF8.GetBytes("{}"), header));

    [Fact]
    public void Tampered_body_or_wrong_secret_fails_github_verification()
    {
        var body = Push("refs/heads/main");
        var signature = Sign(body);

        Assert.False(ProjectWebhooks.VerifyGitHubSignature(Secret, Encoding.UTF8.GetBytes(body + " "), signature));
        Assert.False(ProjectWebhooks.VerifyGitHubSignature("baska", Encoding.UTF8.GetBytes(body), signature));
    }

    [Fact]
    public void GitLab_token_must_match_exactly()
    {
        Assert.True(ProjectWebhooks.VerifyGitLabToken(Secret, Secret));
        Assert.False(ProjectWebhooks.VerifyGitLabToken(Secret, Secret + "x"));
        Assert.False(ProjectWebhooks.VerifyGitLabToken(Secret, null));
        Assert.False(ProjectWebhooks.VerifyGitLabToken(string.Empty, string.Empty));
    }

    [Fact]
    public void Verify_dispatches_on_source_and_rejects_unknown_requests()
    {
        var body = Push("refs/heads/main");

        Assert.True(ProjectWebhooks.Verify(Secret, GitHub("push", body)));
        Assert.True(ProjectWebhooks.Verify(Secret, GitLab("Push Hook", body)));
        Assert.False(ProjectWebhooks.Verify(Secret, new WebhookDelivery(Encoding.UTF8.GetBytes(body))));
    }

    [Fact]
    public void Push_to_project_branch_deploys_the_pushed_commit()
    {
        var decision = ProjectWebhooks.Decide(GitHub("push", Push("refs/heads/main")), "main");

        Assert.Equal(WebhookDecisionKind.Deploy, decision.Kind);
        Assert.Equal(Sha, decision.Commit);
        Assert.Equal("ayse", decision.Pusher);
    }

    [Fact]
    public void Push_to_another_branch_is_ignored()
    {
        var decision = ProjectWebhooks.Decide(GitHub("push", Push("refs/heads/feature/x")), "main");

        Assert.Equal(WebhookDecisionKind.Ignore, decision.Kind);
        Assert.Contains("main", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Tag_push_with_same_name_is_ignored() =>
        Assert.Equal(WebhookDecisionKind.Ignore, ProjectWebhooks.Decide(GitHub("push", Push("refs/tags/main")), "main").Kind);

    [Fact]
    public void Branch_deletion_is_ignored()
    {
        Assert.Equal(WebhookDecisionKind.Ignore, ProjectWebhooks.Decide(GitHub("push", Push("refs/heads/main", deleted: true)), "main").Kind);
        Assert.Equal(WebhookDecisionKind.Ignore, ProjectWebhooks.Decide(GitHub("push", Push("refs/heads/main", new string('0', 40))), "main").Kind);
    }

    [Theory]
    [InlineData("pull_request")]
    [InlineData("release")]
    [InlineData("create")]
    public void Non_push_github_events_are_ignored(string eventName) =>
        Assert.Equal(WebhookDecisionKind.Ignore, ProjectWebhooks.Decide(GitHub(eventName, Push("refs/heads/main")), "main").Kind);

    [Fact]
    public void GitHub_ping_is_answered() =>
        Assert.Equal(WebhookDecisionKind.Ping, ProjectWebhooks.Decide(GitHub("ping", "{\"zen\":\"x\"}"), "main").Kind);

    [Fact]
    public void GitLab_push_hook_uses_checkout_sha()
    {
        var body = $"{{\"object_kind\":\"push\",\"ref\":\"refs/heads/main\",\"after\":\"{Sha}\",\"checkout_sha\":\"{Sha}\",\"user_username\":\"mehmet\"}}";

        var decision = ProjectWebhooks.Decide(GitLab("Push Hook", body), "main");

        Assert.Equal(WebhookDecisionKind.Deploy, decision.Kind);
        Assert.Equal(Sha, decision.Commit);
        Assert.Equal("mehmet", decision.Pusher);
    }

    [Fact]
    public void GitLab_non_push_hooks_are_ignored() =>
        Assert.Equal(WebhookDecisionKind.Ignore, ProjectWebhooks.Decide(GitLab("Merge Request Hook", "{}"), "main").Kind);

    [Fact]
    public void Invalid_commit_in_payload_deploys_branch_head()
    {
        var decision = ProjectWebhooks.Decide(GitHub("push", Push("refs/heads/main", "abc")), "main");

        Assert.Equal(WebhookDecisionKind.Deploy, decision.Kind);
        Assert.Null(decision.Commit);
    }

    [Fact]
    public void Unreadable_payload_is_ignored() =>
        Assert.Equal(WebhookDecisionKind.Ignore, ProjectWebhooks.Decide(GitHub("push", "not json"), "main").Kind);

    [Fact]
    public void Generated_secrets_are_unique_hex()
    {
        var first = ProjectWebhooks.GenerateSecret();

        Assert.Equal(ProjectWebhooks.SecretBytes * 2, first.Length);
        Assert.Matches("^[0-9a-f]+$", first);
        Assert.NotEqual(first, ProjectWebhooks.GenerateSecret());
    }
}
