using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using ServerManager.Application.Auditing;
using ServerManager.Domain.Entities;
using ServerManager.Infrastructure.Security;

namespace ServerManager.Application.Tests.Auditing;

public class AuditChainTests
{
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);

    private static HmacAuditChainSigner CreateSigner(byte[]? key = null) =>
        new(Options.Create(new SecurityOptions { MasterKey = Convert.ToBase64String(key ?? Key), KeyVersion = 1 }));

    private static List<AuditLog> BuildChain(HmacAuditChainSigner signer, int count, int unsignedPrefix = 0)
    {
        var logs = new List<AuditLog>();
        string? previous = null;
        var start = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
        for (var i = 1; i <= count; i++)
        {
            var log = new AuditLog
            {
                Id = i,
                UserId = "u1",
                UserName = "admin",
                Action = AuditActions.ServerUpdate,
                EntityType = AuditEntityTypes.Server,
                EntityId = $"id-{i}",
                TargetName = "web-1",
                Details = i % 2 == 0 ? null : "Ayar değişti",
                IpAddress = "10.0.0.5",
                UserAgent = "test",
                IsSuccess = true,
                CreatedAt = start.AddSeconds(i)
            };
            if (i > unsignedPrefix)
            {
                log.ChainHash = signer.Sign(AuditChainFormat.Canonicalize(previous, log));
                previous = log.ChainHash;
            }
            logs.Add(log);
        }

        return logs;
    }

    private static async IAsyncEnumerable<AuditLog> Stream(IEnumerable<AuditLog> logs)
    {
        foreach (var log in logs)
        {
            await Task.Yield();
            yield return log;
        }
    }

    [Fact]
    public void Canonical_form_distinguishes_null_from_empty_and_resists_separator_injection()
    {
        var a = new AuditLog { Action = "x", Details = null, CreatedAt = DateTime.UtcNow };
        var b = new AuditLog { Action = "x", Details = string.Empty, CreatedAt = a.CreatedAt };
        var c = new AuditLog { Action = "x", TargetName = "a|1:b", CreatedAt = a.CreatedAt };
        var d = new AuditLog { Action = "x", TargetName = "a", EntityId = "b", CreatedAt = a.CreatedAt };

        Assert.NotEqual(AuditChainFormat.Canonicalize(null, a), AuditChainFormat.Canonicalize(null, b));
        Assert.NotEqual(AuditChainFormat.Canonicalize(null, c), AuditChainFormat.Canonicalize(null, d));
    }

    [Fact]
    public void Timestamp_is_normalized_to_milliseconds()
    {
        var value = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc).AddTicks(12_345_678);

        var normalized = AuditChainFormat.NormalizeTimestamp(value);

        Assert.Equal(0, normalized.Ticks % TimeSpan.TicksPerMillisecond);
        Assert.Equal(DateTimeKind.Utc, normalized.Kind);
        Assert.Equal(AuditChainFormat.Canonicalize(null, new AuditLog { Action = "x", CreatedAt = normalized }),
            AuditChainFormat.Canonicalize(null, new AuditLog { Action = "x", CreatedAt = DateTime.SpecifyKind(normalized, DateTimeKind.Unspecified) }));
    }

    [Fact]
    public void Signer_is_deterministic_and_key_dependent()
    {
        var payload = "sm-audit-v1|-|x";

        Assert.Equal(CreateSigner().Sign(payload), CreateSigner().Sign(payload));
        Assert.Matches("^[0-9a-f]{64}$", CreateSigner().Sign(payload));
        Assert.NotEqual(CreateSigner().Sign(payload), CreateSigner(RandomNumberGenerator.GetBytes(32)).Sign(payload));
    }

    [Fact]
    public async Task Intact_chain_with_legacy_prefix_is_valid()
    {
        var signer = CreateSigner();
        var logs = BuildChain(signer, 10, unsignedPrefix: 3);

        var result = await AuditChainVerifier.VerifyAsync(Stream(logs), signer, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.Equal(10, result.CheckedCount);
        Assert.Equal(7, result.SignedCount);
        Assert.Equal(3, result.UnsignedCount);
        Assert.Null(result.BrokenAtId);
    }

    [Fact]
    public async Task Modified_record_breaks_chain_at_that_record()
    {
        var signer = CreateSigner();
        var logs = BuildChain(signer, 8);
        logs[4].Details = "Değiştirildi";

        var result = await AuditChainVerifier.VerifyAsync(Stream(logs), signer, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Equal(5, result.BrokenAtId);
    }

    [Fact]
    public async Task Deleted_record_breaks_chain_at_next_record()
    {
        var signer = CreateSigner();
        var logs = BuildChain(signer, 8);
        logs.RemoveAt(3);

        var result = await AuditChainVerifier.VerifyAsync(Stream(logs), signer, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Equal(5, result.BrokenAtId);
    }

    [Fact]
    public async Task Removed_signature_after_signed_records_is_detected()
    {
        var signer = CreateSigner();
        var logs = BuildChain(signer, 6);
        logs[5].ChainHash = null;

        var result = await AuditChainVerifier.VerifyAsync(Stream(logs), signer, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Equal(6, result.BrokenAtId);
    }

    [Fact]
    public async Task Chain_signed_with_another_key_is_invalid()
    {
        var logs = BuildChain(CreateSigner(RandomNumberGenerator.GetBytes(32)), 3);

        var result = await AuditChainVerifier.VerifyAsync(Stream(logs), CreateSigner(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Equal(1, result.BrokenAtId);
    }

    private static AuditChainAnchor AnchorFor(HmacAuditChainSigner signer, List<AuditLog> logs)
    {
        AuditChainAnchor? anchor = null;
        foreach (var log in logs.Where(l => l.ChainHash is not null))
            anchor = AuditChainVerifier.Advance(anchor, log, signer, log.CreatedAt);
        return anchor!;
    }

    [Fact]
    public async Task Intact_chain_matching_anchor_is_valid()
    {
        var signer = CreateSigner();
        var logs = BuildChain(signer, 10, unsignedPrefix: 2);
        var anchor = AnchorFor(signer, logs);

        var result = await AuditChainVerifier.VerifyAsync(Stream(logs), signer, anchor, anchorExpected: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsValid, result.Message);
        Assert.Equal(3, anchor.FirstSignedId);
        Assert.Equal(8, anchor.SignedCount);
        Assert.Equal(10, anchor.LastId);
    }

    [Fact]
    public async Task Records_appended_after_anchor_was_read_are_valid()
    {
        var signer = CreateSigner();
        var logs = BuildChain(signer, 10);
        var anchor = AnchorFor(signer, logs.Take(7).ToList());

        var result = await AuditChainVerifier.VerifyAsync(Stream(logs), signer, anchor, anchorExpected: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsValid, result.Message);
        Assert.Equal(10, result.SignedCount);
    }

    [Fact]
    public async Task Deleted_tail_is_detected_by_anchor()
    {
        var signer = CreateSigner();
        var logs = BuildChain(signer, 8);
        var anchor = AnchorFor(signer, logs);
        logs.RemoveRange(6, 2);

        var withoutAnchor = await AuditChainVerifier.VerifyAsync(Stream(logs), signer, cancellationToken: TestContext.Current.CancellationToken);
        var withAnchor = await AuditChainVerifier.VerifyAsync(Stream(logs), signer, anchor, anchorExpected: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(withoutAnchor.IsValid);
        Assert.False(withAnchor.IsValid);
        Assert.Equal(8, withAnchor.BrokenAtId);
    }

    [Fact]
    public async Task Wiped_table_is_detected_by_anchor()
    {
        var signer = CreateSigner();
        var anchor = AnchorFor(signer, BuildChain(signer, 5));

        var result = await AuditChainVerifier.VerifyAsync(Stream([]), signer, anchor, anchorExpected: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Stripped_signatures_after_signing_started_are_detected()
    {
        var signer = CreateSigner();
        var logs = BuildChain(signer, 6, unsignedPrefix: 2);
        var anchor = AnchorFor(signer, logs);
        foreach (var log in logs)
            log.ChainHash = null;

        var withoutAnchor = await AuditChainVerifier.VerifyAsync(Stream(logs), signer, cancellationToken: TestContext.Current.CancellationToken);
        var withAnchor = await AuditChainVerifier.VerifyAsync(Stream(logs), signer, anchor, anchorExpected: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(withoutAnchor.IsValid);
        Assert.False(withAnchor.IsValid);
        Assert.Equal(3, withAnchor.BrokenAtId);
    }

    [Fact]
    public async Task Tampered_anchor_is_rejected()
    {
        var signer = CreateSigner();
        var logs = BuildChain(signer, 6);
        var anchor = AnchorFor(signer, logs.Take(4).ToList());
        anchor.LastId = 6;
        anchor.LastHash = logs[5].ChainHash!;
        anchor.SignedCount = 6;

        var result = await AuditChainVerifier.VerifyAsync(Stream(logs), signer, anchor, anchorExpected: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Null(result.BrokenAtId);
    }

    [Fact]
    public async Task Missing_anchor_with_signed_records_is_detected_when_expected()
    {
        var signer = CreateSigner();
        var logs = BuildChain(signer, 4);

        var result = await AuditChainVerifier.VerifyAsync(Stream(logs), signer, anchor: null, anchorExpected: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Legacy_unsigned_log_without_anchor_is_valid_when_expected()
    {
        var signer = CreateSigner();
        var logs = BuildChain(signer, 3, unsignedPrefix: 3);

        var result = await AuditChainVerifier.VerifyAsync(Stream(logs), signer, anchor: null, anchorExpected: true, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Anchor_created_from_summary_matches_advanced_anchor()
    {
        var signer = CreateSigner();
        var logs = BuildChain(signer, 5, unsignedPrefix: 1);
        var advanced = AnchorFor(signer, logs);

        var created = AuditChainVerifier.Create(
            new AuditChainSummary(2, logs[1].CreatedAt, 5, logs[4].ChainHash!, 4), signer, DateTime.UtcNow);

        Assert.Equal(advanced.Signature, created.Signature);
        Assert.True(AuditChainVerifier.IsAnchorSignatureValid(created, signer));
    }

    [Fact]
    public async Task Empty_log_is_valid()
    {
        var result = await AuditChainVerifier.VerifyAsync(Stream([]), CreateSigner(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.Equal(0, result.CheckedCount);
    }
}
