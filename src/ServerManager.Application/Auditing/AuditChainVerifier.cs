using System.Security.Cryptography;
using System.Text;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.Interfaces.Security;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Auditing;

public static class AuditChainVerifier
{
    /// <summary>
    /// Kayıtlar Id sırasıyla gelmelidir. İmzasız kayıtlar yalnızca zincirin başında (özellik öncesi) kabul edilir;
    /// imzalı kayıtlardan sonra gelen imzasız kayıt, silinmiş imza olarak değerlendirilir.
    /// <para>
    /// <paramref name="anchor"/> verilirse (kayıtlardan önce okunmuş olmalı): çapa imzası doğrulanır, imzalama başladıktan
    /// sonraki imzasız kayıtlar reddedilir ve çapadaki son kayda aynı imza ve sayıyla ulaşılması beklenir. Böylece zincirin
    /// sonundan kayıt silinmesi veya tablonun boşaltılması da yakalanır. Doğrulama sırasında eklenen yeni kayıtlar çapanın
    /// ötesinde kalır ve zincirin devamı olarak doğrulanır.
    /// </para>
    /// </summary>
    /// <param name="anchorExpected">Çapa tutulan sistemlerde true; imzalı kayıt varken çapanın olmaması silinme sayılır.</param>
    public static async Task<AuditChainVerificationDto> VerifyAsync(
        IAsyncEnumerable<AuditLog> logs,
        IAuditChainSigner signer,
        AuditChainAnchor? anchor = null,
        bool anchorExpected = false,
        CancellationToken cancellationToken = default)
    {
        if (anchor is not null && !IsAnchorSignatureValid(anchor, signer))
            return Broken(null, null, 0, 0, 0, "Zincir çapasının imzası geçersiz (çapa değiştirilmiş olabilir).");

        string? previousHash = null;
        var checkedCount = 0;
        var signedCount = 0;
        var unsignedCount = 0;
        var anchorReached = anchor is null;

        await foreach (var log in logs.WithCancellation(cancellationToken))
        {
            checkedCount++;

            if (log.ChainHash is null)
            {
                if (signedCount > 0)
                    return Broken(log, checkedCount, signedCount, unsignedCount, "İmzalı kayıtlardan sonra imzasız kayıt var (imza silinmiş olabilir).");

                if (anchor is not null && log.Id >= anchor.FirstSignedId)
                    return Broken(log, checkedCount, signedCount, unsignedCount, "İmzalama başladıktan sonra yazılmış imzasız kayıt var (imza silinmiş olabilir).");

                unsignedCount++;
                continue;
            }

            if (!anchorReached && log.Id > anchor!.LastId)
                return Broken(log, checkedCount, signedCount, unsignedCount, $"Çapadaki son kayıt (#{anchor.LastId}) bulunamadı; kayıt silinmiş olabilir.");

            var expected = signer.Sign(AuditChainFormat.Canonicalize(previousHash, log));
            if (!FixedTimeEquals(expected, log.ChainHash))
                return Broken(log, checkedCount, signedCount, unsignedCount, "Kayıt değiştirilmiş, silinmiş ya da araya kayıt eklenmiş.");

            signedCount++;
            previousHash = log.ChainHash;

            if (!anchorReached && log.Id == anchor!.LastId)
            {
                if (!FixedTimeEquals(anchor.LastHash, log.ChainHash) || signedCount != anchor.SignedCount)
                    return Broken(log, checkedCount, signedCount, unsignedCount, "Zincir çapası ile uyuşmuyor; aradan kayıt silinmiş olabilir.");

                anchorReached = true;
            }
        }

        if (!anchorReached)
        {
            return Broken(anchor!.LastId, null, checkedCount, signedCount, unsignedCount,
                $"Zincirin sonundan kayıt silinmiş olabilir: çapadaki son kayıt (#{anchor.LastId}, toplam {anchor.SignedCount} imzalı kayıt) bulunamadı.");
        }

        if (anchor is null && anchorExpected && signedCount > 0)
            return Broken(null, null, checkedCount, signedCount, unsignedCount, "İmzalı kayıtlar var fakat zincir çapası bulunamadı (çapa silinmiş olabilir).");

        return new AuditChainVerificationDto
        {
            IsValid = true,
            CheckedCount = checkedCount,
            SignedCount = signedCount,
            UnsignedCount = unsignedCount,
            Message = signedCount == 0
                ? "Henüz imzalı kayıt yok."
                : $"{signedCount} imzalı kaydın zinciri sağlam."
        };
    }

    public static bool IsAnchorSignatureValid(AuditChainAnchor anchor, IAuditChainSigner signer) =>
        !string.IsNullOrEmpty(anchor.Signature)
        && FixedTimeEquals(signer.Sign(AuditChainFormat.CanonicalizeAnchor(anchor)), anchor.Signature);

    /// <summary>Yeni imzalı kayıt sonrası çapanın yeni hali (imzalanmış). Çapa yoksa ilk imzalı kayıttan başlatılır.</summary>
    public static AuditChainAnchor Advance(AuditChainAnchor? current, AuditLog appended, IAuditChainSigner signer, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(appended.ChainHash);

        var next = new AuditChainAnchor
        {
            Id = AuditChainAnchor.SingletonId,
            FirstSignedId = current?.FirstSignedId ?? appended.Id,
            SigningStartedAt = current?.SigningStartedAt ?? appended.CreatedAt,
            LastId = appended.Id,
            LastHash = appended.ChainHash,
            SignedCount = (current?.SignedCount ?? 0) + 1,
            UpdatedAt = now
        };
        next.Signature = signer.Sign(AuditChainFormat.CanonicalizeAnchor(next));
        return next;
    }

    /// <summary>Çapa özelliğinden önce imzalanmış kayıtlar için ilk çapa (mevcut zincirin son hali).</summary>
    public static AuditChainAnchor Create(AuditChainSummary summary, IAuditChainSigner signer, DateTime now)
    {
        var anchor = new AuditChainAnchor
        {
            Id = AuditChainAnchor.SingletonId,
            FirstSignedId = summary.FirstSignedId,
            SigningStartedAt = summary.FirstSignedAt,
            LastId = summary.LastSignedId,
            LastHash = summary.LastHash,
            SignedCount = summary.SignedCount,
            UpdatedAt = now
        };
        anchor.Signature = signer.Sign(AuditChainFormat.CanonicalizeAnchor(anchor));
        return anchor;
    }

    private static bool FixedTimeEquals(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(actual));

    private static AuditChainVerificationDto Broken(AuditLog log, int checkedCount, int signedCount, int unsignedCount, string reason) =>
        Broken(log.Id, log.CreatedAt, checkedCount, signedCount, unsignedCount, reason);

    private static AuditChainVerificationDto Broken(long? id, DateTime? time, int checkedCount, int signedCount, int unsignedCount, string reason) => new()
    {
        IsValid = false,
        CheckedCount = checkedCount,
        SignedCount = signedCount,
        UnsignedCount = unsignedCount,
        BrokenAtId = id,
        BrokenAtTime = time,
        Message = id is null
            ? $"Zincir doğrulanamadı. {reason}"
            : $"Zincir #{id} numaralı kayıtta kırılıyor. {reason}"
    };
}

/// <summary>Mevcut imzalı kayıtların özeti (ilk çapa oluşturulurken kullanılır).</summary>
public sealed record AuditChainSummary(long FirstSignedId, DateTime FirstSignedAt, long LastSignedId, string LastHash, long SignedCount);
