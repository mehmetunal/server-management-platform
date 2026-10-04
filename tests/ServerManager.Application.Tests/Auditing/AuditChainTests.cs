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

        var result = await AuditChainVerifier.VerifyAsync(Stream(logs), signer);

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

        var result = await AuditChainVerifier.VerifyAsync(Stream(logs), signer);

        Assert.False(result.IsValid);
        Assert.Equal(5, result.BrokenAtId);
    }

    [Fact]
    public async Task Deleted_record_breaks_chain_at_next_record()
    {
        var signer = CreateSigner();
        var logs = BuildChain(signer, 8);
        logs.RemoveAt(3);

        var result = await AuditChainVerifier.VerifyAsync(Stream(logs), signer);

        Assert.False(result.IsValid);
        Assert.Equal(5, result.BrokenAtId);
    }

    [Fact]
    public async Task Removed_signature_after_signed_records_is_detected()
    {
        var signer = CreateSigner();
        var logs = BuildChain(signer, 6);
        logs[5].ChainHash = null;

        var result = await AuditChainVerifier.VerifyAsync(Stream(logs), signer);

        Assert.False(result.IsValid);
        Assert.Equal(6, result.BrokenAtId);
    }

    [Fact]
    public async Task Chain_signed_with_another_key_is_invalid()
    {
        var logs = BuildChain(CreateSigner(RandomNumberGenerator.GetBytes(32)), 3);

        var result = await AuditChainVerifier.VerifyAsync(Stream(logs), CreateSigner());

        Assert.False(result.IsValid);
        Assert.Equal(1, result.BrokenAtId);
    }

    [Fact]
    public async Task Empty_log_is_valid()
    {
        var result = await AuditChainVerifier.VerifyAsync(Stream([]), CreateSigner());

        Assert.True(result.IsValid);
        Assert.Equal(0, result.CheckedCount);
    }
}
